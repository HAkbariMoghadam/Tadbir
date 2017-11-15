using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TadbirBot
{
    public class MSCRMManager
    {
        public static OrganizationServiceProxy GetOrgService()
        {
            Uri orgUri;

            string crmUrl = System.Configuration.ConfigurationManager.AppSettings["mscrm_url"].ToString();
            string crmClaimUrl = System.Configuration.ConfigurationManager.AppSettings["mscrm_claim_url"].ToString();
            Boolean claimBaseAthu = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["mscrm_claimBaseAthu"]);

            string UserName = System.Configuration.ConfigurationManager.AppSettings["mscrm_user_name"].ToString();
            string Password = System.Configuration.ConfigurationManager.AppSettings["mscrm_user_pass"].ToString();
            string Domain = System.Configuration.ConfigurationManager.AppSettings["mscrm_user_domain"].ToString();

            if (!claimBaseAthu)
                orgUri = new Uri(string.Format("{0}/XRMServices/2011/Organization.svc", crmUrl));
            else
                orgUri = new Uri(string.Format("{0}/XRMServices/2011/Organization.svc", crmClaimUrl));

            System.ServiceModel.Description.ClientCredentials _ClientCredentials = new System.ServiceModel.Description.ClientCredentials();
            _ClientCredentials.Windows.ClientCredential = new System.Net.NetworkCredential(UserName, Password, Domain);
            OrganizationServiceProxy _OrganizationServiceProxy = new OrganizationServiceProxy(orgUri, null, _ClientCredentials, null);
            return _OrganizationServiceProxy;

        }

        public static Boolean isAuthorizedUser(OrganizationServiceProxy orgService, String mobilePhone)
        {
            bool authorize = false;

            QueryExpression queryExpression = new QueryExpression("contact");
            queryExpression.ColumnSet = new ColumnSet(new String[] { "contactid", "mobilephone", "parentcustomerid", "new_accesstotickets" });
            queryExpression.Criteria.AddCondition("mobilephone", ConditionOperator.Equal, mobilePhone);
            queryExpression.Criteria.AddCondition("new_accesstotickets", ConditionOperator.Equal, true);
            queryExpression.Criteria.AddCondition("parentcustomerid", ConditionOperator.NotNull);
            EntityCollection entityCollection = orgService.RetrieveMultiple(queryExpression);

            if (entityCollection.Entities.Count > 0)
                authorize = true;

            return authorize;
        }

        public static string createCase(String mobilePhone, string productSubject, string title, string caseDescription, string body, string filename, int filesize, string mimetype)
        {
            OrganizationServiceProxy orgService = GetOrgService();
            string message = "";
            bool authorize = isAuthorizedUser(orgService, mobilePhone);

            if (authorize)
            {
                List<string> products = getProductSubjects(mobilePhone);
                string product = products.FirstOrDefault(t => t.Contains(productSubject));

                if (product != null)
                {
                    Entity contact = getRelatedEntity(orgService, "contact", new ColumnSet(new String[] { "contactid", "mobilephone", "parentcustomerid", "new_accesstotickets" }), "mobilephone", mobilePhone);

                    //get selected product subject
                    Entity productSubjectEn = getRelatedEntity(orgService, "new_productsubject", new ColumnSet(new String[] { "new_productsubjectid", "new_name" }), "new_name", productSubject);

                    Entity caseEn = new Entity("incident");
                    caseEn["title"] = title;
                    caseEn["new_productsubject"] = new EntityReference("new_productsubject", productSubjectEn.Id);
                    caseEn["customerid"] = contact["parentcustomerid"];
                    caseEn["primarycontactid"] = new EntityReference("contact", contact.Id);
                    caseEn["caseorigincode"] = new OptionSetValue(5);
                    caseEn["casetypecode"] = new OptionSetValue(3);//Question = 1, Problem = 2, Request = 3
                    caseEn["new_showinsite"] = true;
                    Guid caseId = orgService.Create(caseEn);

                    //create note
                    CreateNotes(body, filename, filesize, mimetype, caseId, caseDescription);

                    Entity incident = orgService.Retrieve("incident", caseId, new ColumnSet(new string[] { "incidentid", "ticketnumber" }));

                    if (incident.Contains("ticketnumber"))
                        message = "تیکت شما به شماره " + (string)incident["ticketnumber"] + "ثبت گردید.";
                    else
                        message = "تیکت با موفقیت ثبت گردید";
                }
                else
                    message = "برای محصول انتخابی مجوز ثبت تیکت ندارید، لطفاً با واحد پشتیبانی تماس بگیرید";

            }
            else
                message = "شما مجوز ثبت تیکت را ندارید، لطفاً با واحد پشتیبانی تماس بگیرید";

            return message;
        }

        public static string getCaseStatus(string mobilePhone, string ticketNumber)
        {
            string message = "";
            try
            {
                OrganizationServiceProxy orgService = GetOrgService();
                
                bool authorize = isAuthorizedUser(orgService, mobilePhone);

                if (authorize)
                {
                    Entity contact = getRelatedEntity(orgService, "contact", new ColumnSet(new String[] { "contactid", "mobilephone", "parentcustomerid", "new_accesstotickets" }), "mobilephone", mobilePhone);
                    if (contact != null)
                    {

                        QueryExpression queryExpression = new QueryExpression("incident");
                        queryExpression.ColumnSet = new ColumnSet(new String[] { "incidentid", "customerid", "ticketnumber", "statecode", "title" });
                        queryExpression.Criteria.AddCondition("ticketnumber", ConditionOperator.Equal, ticketNumber);
                        queryExpression.Criteria.AddCondition("customerid", ConditionOperator.Equal, ((EntityReference)contact["parentcustomerid"]).Id);

                        EntityCollection entityCollection = orgService.RetrieveMultiple(queryExpression);

                        if (entityCollection.Entities.Count > 0)
                        {
                            Entity caseEn = entityCollection.Entities[0];
                            int caseStatus = ((OptionSetValue)caseEn["statecode"]).Value;

                            switch (caseStatus)
                            {
                                case 1://resolve
                                    message = "تیکت شما با موضوع " + (string)caseEn["title"] + " در وضعیت 'حل شده' قرار گرفت";
                                    break;
                                case 2://cancel
                                    message = "تیکت شما با موضوع " + (string)caseEn["title"] + " در وضعیت 'لغو شده' قرار گرفت";
                                    break;
                                case 0://Active
                                    {
                                        Entity lstNote = getLastNote(orgService, caseEn.Id);
                                        if (lstNote != null)
                                            message = "همکار گرامی: تیکت شما در وضعیت فعال قرار دارد" + Environment.NewLine + "آخرین یادداشت ثبت شده: " + Environment.NewLine + ((String)lstNote["notetext"]).Remove(0, 2);
                                        else
                                            message = "تیکت شما با موضوع " + (string)caseEn["title"] + " در وضعیت 'فعال' قرار دارد";
                                    }
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
                else
                    message = "شما مجوز دریافت وضعیت تیکت را ندارید، لطفاً با واحد پشتیبانی تماس بگیرید";          
            }
            catch
            {
                message = "خطا در دریافت وضعیت تیکت";
            }
            return message;
        }

        public static void CreateNotes( string body, string filename, int filesize, string mimetype, Guid objectid, string nottext)
        {
            OrganizationServiceProxy orgService = GetOrgService();
            if (body != "" && nottext == "")
            {
                Entity note = new Entity("annotation");
                note["documentbody"] = body;
                note["filename"] = filename;
                note["filesize"] = filesize;
                note["mimetype"] = mimetype;
                note["isdocument"] = true;
                note["objecttypecode"] = "incident";
                note["objectid"] = new EntityReference("incident", objectid);
                orgService.Create(note);
            }
            if (nottext != "" && body == "")
            {
                Entity note = new Entity("annotation");
                note["notetext"] = nottext;
                note["objecttypecode"] = "incident";
                note["objectid"] = new EntityReference("incident", objectid);
                orgService.Create(note);
            }
            if (nottext != "" && body != "")
            {
                Entity note = new Entity("annotation");
                note["documentbody"] = body;
                note["filename"] = filename;
                note["filesize"] = filesize;
                note["mimetype"] = mimetype;
                note["isdocument"] = true;
                note["notetext"] = nottext;
                note["objecttypecode"] = "incident";
                note["objectid"] = new EntityReference("incident", objectid);
                orgService.Create(note);
            }

        }

        private static Entity getLastNote(IOrganizationService orgService, Guid caseId)
        {
            Entity entity = null;

            //Entity supportUser = orgService.Retrieve("systemuser", new Guid("199EC19A-0EDA-E111-A2B1-000C29F8DE1E"), new ColumnSet(new String[] { "systemuserid" }));
            //Entity SDKUser = orgService.Retrieve("systemuser", new Guid("47DAEF69-385C-E411-A947-000C29F8DE1E"), new ColumnSet(new String[] { "systemuserid" }));

            QueryExpression queryExpression = new QueryExpression("annotation");
            queryExpression.ColumnSet = new ColumnSet(new String[] { "objectid", "notetext", "createdon", "createdby" });
            queryExpression.Criteria.AddCondition("notetext", ConditionOperator.BeginsWith, "##");
            queryExpression.Criteria.AddCondition("createdby", ConditionOperator.NotIn, new Guid("199EC19A-0EDA-E111-A2B1-000C29F8DE1E"), new Guid("199EC19A-0EDA-E111-A2B1-000C29F8DE1E"));
            queryExpression.Criteria.AddCondition("objectid", ConditionOperator.Equal, caseId);
            queryExpression.AddOrder("createdon", OrderType.Descending);

            EntityCollection entityCollection = orgService.RetrieveMultiple(queryExpression);

            if (entityCollection.Entities.Count > 0)
                entity = entityCollection.Entities[0];

            return entity;
        }

        public static List<String> getProductSubjects( string mobilePhone)
        {
            OrganizationServiceProxy orgService = GetOrgService();
            List<string> products = null;
            bool authorize = isAuthorizedUser(orgService, mobilePhone);

            if (authorize)
            {
                Entity contact = getRelatedEntity(orgService, "contact", new ColumnSet(new String[] { "contactid", "mobilephone", "parentcustomerid", "new_accesstotickets" }), "mobilephone", mobilePhone);

                QueryExpression query = new QueryExpression("new_productsubject");
                query.ColumnSet = new ColumnSet(true);
                LinkEntity linkEntity1 = new LinkEntity("new_productsubject", "new_contact_new_productsubjects", "new_productsubjectid", "new_productsubjectid", JoinOperator.Inner);
                LinkEntity linkEntity2 = new LinkEntity("new_contact_new_productsubjects", "contact", "contactid", "contactid", JoinOperator.Inner);
                linkEntity1.LinkEntities.Add(linkEntity2);
                query.LinkEntities.Add(linkEntity1);
                linkEntity2.LinkCriteria = new FilterExpression();

                linkEntity2.LinkCriteria.AddCondition(new ConditionExpression("contactid", ConditionOperator.Equal, contact.Id));

                EntityCollection entities = orgService.RetrieveMultiple(query);
                if (entities.Entities.Count > 0)
                {
                    for (int i = 0; i < entities.Entities.Count; i++)
                    {
                        Entity productSubject = entities.Entities[i];
                        products.Add((String)productSubject["new_name"]);
                    }
                }
            }
            return products;
        }

        private static Entity getRelatedEntity(IOrganizationService orgService, string entityName, Microsoft.Xrm.Sdk.Query.ColumnSet entityAttributes, string attributeName, string attributeValue)
        {
            Entity entity = null;
            QueryExpression queryExpression = new QueryExpression(entityName);
            queryExpression.ColumnSet = entityAttributes;
            queryExpression.Criteria.AddCondition(attributeName, ConditionOperator.Equal, attributeValue);
            EntityCollection entityCollection = orgService.RetrieveMultiple(queryExpression);

            if (entityCollection.Entities.Count > 0)
                entity = entityCollection.Entities[0];

            return entity;
        }
    }
}