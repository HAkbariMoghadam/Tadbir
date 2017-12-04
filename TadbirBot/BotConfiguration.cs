using RestSharp;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Web;
using TadbirBot.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputMessageContents;
using Telegram.Bot.Types.InlineKeyboardButtons;

namespace TadbirBot
{
    public class BotConfiguration
    {
        internal static readonly string BotAPI = ConfigurationManager.AppSettings["BotApiToken"];
        internal static readonly TelegramBotClient Bot = new TelegramBotClient(BotAPI);
        internal static List<UserInfo> OnlineUsers = new List<UserInfo>();

        public BotConfiguration()
        {
            Bot.OnMessage += Bot_OnMessage;

            Bot.StartReceiving();
        }

        private void Bot_OnMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            try
            {
                var message = e.Message;

                if (message.Chat.Type == ChatType.Group)
                {
                    Bot.SendTextMessageAsync(message.Chat.Id, "متأسفانه آمادگی فعالیت در گروه رو ندارم");
                    return;
                }

                var user = OnlineUsers.FirstOrDefault(t => t.TelegramId == message.From.Id);
                if (user == null)
                {
                    user = new UserInfo
                    {
                        TelegramId = message.From.Id,
                        TelegramName = message.From.Username,
                        FirstName = message.From.FirstName,
                        LastName = message.From.LastName,
                        UserState = UserState.MainMenu
                    };

                    OnlineUsers.Add(user);
                }

                if (IsFixContent(message, user))
                {
                    return;
                }

                if (!DecisionMaker(message, user))
                {
                    ResetUser(ref user);
                    CreateMainMenu(message, user);
                    SendMessageToClient(message, "لطفاً دوباره امتحان کنید");

                    return;
                }

            }
            catch (Exception)
            {

            }
        }

        private bool IsFixContent(Message message, UserInfo user)
        {
            var result = false;
            try
            {
                switch (message.Text)
                {
                    case "/start":
                    case "شروع دوباره":
                        ResetUser(ref user);
                        break;

                    case "پیگیری":
                        user.UserState = UserState.CaseStatus;
                        break;
                    case "درخواست جدید":
                        user.UserState = UserState.NewCase;
                        break;
                    case "تماس با ما":
                        user.UserState = UserState.ContactUs;

                        break;
                }
            }
            catch (Exception)
            {
                result = false;
            }

            return result;
        }

        private void ResetUser(ref UserInfo user)
        {
            user.UserState = UserState.MainMenu;
        }

        private void ShowContactUs(Message message)
        {
            Bot.SendLocationAsync(message.Chat.Id, (float)35.7210427, (float)51.4249957);
            Thread.Sleep(100);

            var url = InlineKeyboardButton.WithUrl("سایت تدبیر پرداز", "https://www.etadbir.com");


            Bot.SendTextMessageAsync(message.Chat.Id, @"
همواره پاسخ گو شما هستیم
📞  021-89306767
", replyMarkup:
                new InlineKeyboardMarkup(new[]
                {
                    url
                }));
        }

        private bool DecisionMaker(Message message, UserInfo user)
        {
            var result = false;
            try
            {
                switch (user.UserState)
                {
                    case UserState.MainMenu:
                        CreateMainMenu(message, user);
                        break;
                    case UserState.CaseStatus:
                        SendMessageToClient(message, "لطفا شماره پیگیری درخواست خود را به صورت صحیح وارد کنید.", RestartKeyboard());
                        user.UserState = UserState.EnterCaseNumber;
                        break;
                    case UserState.EnterCaseNumber:
                        if (string.IsNullOrWhiteSpace(message.Text) == false)
                        {
                            user.CaseId = message.Text;
                            user.UserState = UserState.EnterContactForStatus;
                            SendMessageToClient(message, "لطفا شماره تلفنی که با آن درخواست را ثبت کرده اید از طریق کلید ارسال شماره ارائه دهید.", ContactKeyboard());
                        }
                        else
                        {
                            SendMessageToClient(message, "لطفا شماره پیگیری درخواست خود را به صورت صحیح وارد کنید.", RestartKeyboard());
                        }
                        break;
                    case UserState.EnterContactForStatus:
                        if ((message != null && message.Contact != null && string.IsNullOrWhiteSpace(message.Contact.PhoneNumber) == false))
                        {
                            user.UserNumber = CorrectPhone(message.Contact?.PhoneNumber);
                            SendMessageToClient(message, "لطفا منتظر بمانید....", RestartKeyboard());
                            Bot.SendChatActionAsync(message.From.Id, ChatAction.Typing);
                            user.CaseStatus = GetCaseStatus(user);
                            //SendMessageToClient(message, "درخواست شما در وضعیت زیر است.", RestartKeyboard());
                            SendMessageToClient(message, user.CaseStatus, RestartKeyboard());
                        }
                        else
                        {
                            SendMessageToClient(message, "لطفا شماره تلفنی که با آن درخواست را ثبت کرده اید از طریق کلید ارسال شماره ارائه دهید.", ContactKeyboard());
                        }
                        break;
                    case UserState.NewCase:
                        user.UserState = UserState.EnterContactForNew;
                        SendMessageToClient(message, "لطفا شماره تلفنی که میخواهید با آن درخواست را ثبت کنید از طریق کلید ارسال شماره ارائه دهید.", ContactKeyboard());
                        break;
                    case UserState.EnterContactForNew:
                        if ((message != null && message.Contact != null && string.IsNullOrWhiteSpace(message.Contact.PhoneNumber) == false))
                        {
                            user.UserNumber = CorrectPhone(message.Contact?.PhoneNumber);

                            CreateProductsForUser(message, user);
                        }
                        else
                        {
                            SendMessageToClient(message, "لطفا شماره تلفنی که میخواهید با آن درخواست را ثبت کنید از طریق کلید ارسال شماره ارائه دهید.", ContactKeyboard());
                        }
                        break;
                    case UserState.EnterProduct:
                        if (string.IsNullOrWhiteSpace(message.Text) == false)
                        {
                            user.CaseProduct = message.Text;
                            user.UserState = UserState.EnterTitle;
                            SendMessageToClient(message, "لطفا موضوع تیکت را وارد نمایید.", RestartKeyboard());
                        }
                        else
                        {
                            CreateProductsForUser(message, user);
                        }
                        break;
                    case UserState.EnterTitle:
                        if (string.IsNullOrWhiteSpace(message.Text) == false)
                        {
                            user.CaseTitle = message.Text;
                            user.UserState = UserState.EnterCaseDescription;
                            SendMessageToClient(message, "لطفا توضیحات مورد نیاز برای تیکت را وارد نمایید.", RestartKeyboard());
                        }
                        else
                        {
                            SendMessageToClient(message, "لطفا موضوع تیکت را وارد نمایید.", RestartKeyboard());
                        }
                        break;
                    case UserState.EnterCaseDescription:
                        if (string.IsNullOrWhiteSpace(message.Text) == false)
                        {
                            user.CaseDescription = message.Text;
                            user.UserState = UserState.EnterCaseAttachment;
                            SendMessageToClient(message, "لطفا فایل پیوست تیکت (حداکثر 5 مگابایت) در صورت وجود بارگزاری کنید.", CreateNoDocKeyboeard());
                        }
                        else
                        {
                            SendMessageToClient(message, "لطفا توضیحات مورد نیاز برای تیکت را وارد نمایید.", RestartKeyboard());
                        }
                        break;
                    case UserState.EnterCaseAttachment:
                        if ((string.IsNullOrWhiteSpace(message.Text) || message.Text == "پیوست ندارد") || (message.Document != null || message.Photo != null))
                        {
                            if (message.Document != null)
                            {

                                var file = DownloadFileById(message.Document.FileId);
                                user.CaseAttachment = file.Buffer;
                                user.CaseFileSize = message.Document.FileSize;
                                user.CaseFileName = message.Document.FileName;
                                user.CaseFileType = "application/ms-infopath.xml";


                            }
                            else if (message.Photo != null)
                            {
                                var file = DownloadFileById(message.Photo.First().FileId);
                                user.CaseAttachment = file.Buffer;
                                user.CaseFileName = string.IsNullOrWhiteSpace(file.FileName) ? $"pic{user.CaseTitle + file.FileExtension}" : file.FileName;
                                user.CaseFileSize = file.FileSize;
                                user.CaseFileType = "image/xyz";
                            }

                            SendMessageToClient(message, "لطفا منتظر بمانید....", RestartKeyboard());
                            Bot.SendChatActionAsync(message.From.Id, ChatAction.Typing);
                            var responseMessage = CreateCase(user);
                            //SendMessageToClient(message, "درخواست شما در وضعیت زیر است.", RestartKeyboard());
                            //SendMessageToClient(message, string.IsNullOrWhiteSpace(user.CaseId) ? "درخواست شما با مشکل مواجه شده است لطفا دوباره تلاش کنید" : $"درخواست شما با موفقیت با شماره {user.CaseId} ثبت شده است", RestartKeyboard());
                            SendMessageToClient(message, responseMessage, RestartKeyboard());
                        }
                        else
                        {
                            SendMessageToClient(message, "لطفا فایل پیوست تیکت (حداکثر 5 مگابات) در صورت وجود بارگزاری کنید.", CreateNoDocKeyboeard());
                        }
                        break;
                    case UserState.ContactUs:
                        ShowContactUs(message);
                        ResetUser(ref user);
                        CreateMainMenu(message, user);
                        break;
                    default:
                        break;
                }

                result = true;
            }
            catch (Exception ex)
            {
                result = false;
                //throw;
            }
            return result;
        }

        internal static FileDownloadResult DownloadFileById(string fileId)
        {
            if (string.IsNullOrEmpty(fileId))
                throw new ArgumentNullException(nameof(fileId));

            try
            {
                var fileInfo = GetFileInfoByFileId(fileId);

                if (string.IsNullOrEmpty(fileInfo.FilePath))
                    return null;

                using (var wc = new WebClient())
                {
                    string downloadUrl = $"https://api.telegram.org/file/bot{BotAPI}/{fileInfo.FilePath}";
                    using (var fileStream = new MemoryStream(wc.DownloadData(downloadUrl)))
                        return new FileDownloadResult
                        {
                            FileId = fileInfo.FileId,
                            FilePath = fileInfo.FilePath,
                            FileExtension = Path.GetExtension(fileInfo.FilePath),
                            FileSize = fileInfo.FileSize,
                            Buffer = fileStream.ToArray()
                        };
                }
            }
            catch
            {
                return null;
            }
        }

        internal static Tfile GetFileInfoByFileId(string fileId)
        {
            if (string.IsNullOrEmpty(fileId))
                throw new ArgumentNullException(nameof(fileId));

            var client = new RestClient("https://api.telegram.org/bot" + BotAPI);
            client.AddDefaultHeader("Content-Type", "application/x-www-form-urlencoded ; charset=UTF-8");
            var request = GenerateRestRequest("getFile", Method.POST, null, new Dictionary<string, object>
            {
                {"file_id", fileId}
            });

            var restResponse = client.Execute<Tfile>(request);
            return restResponse.Data;
        }

        internal static RestRequest GenerateRestRequest(
                                                       string resource,
                                                       Method method,
                                                       Dictionary<string, string> headers = null,
                                                       Dictionary<string, object> parameters = null,
                                                       List<Tuple<string, byte[], string>> files = null)
        {
            var request = new RestRequest(resource, method)
            {
                RootElement = "result"
            };

            if (headers != null)
                foreach (var header in headers)
                    request.AddHeader(header.Key, header.Value);

            if (parameters != null)
                foreach (var parameter in parameters)
                    request.AddParameter(parameter.Key, parameter.Value);

            if (files != null)
                foreach (var file in files)
                    request.AddFile(file.Item1, file.Item2, file.Item3);

            return request;
        }

        private ReplyKeyboardMarkup CreateNoDocKeyboeard()
        {
            var keyboard = new ReplyKeyboardMarkup(new[]
                {
                new [] // first row
                    {
                        new KeyboardButton("پیوست ندارد"),
                    },
                new [] // second row
                    {
                        new KeyboardButton("شروع دوباره"),
                    }
                });

            return keyboard;
        }

        private string CreateCase(UserInfo user)
        {
            if (user.CaseAttachment != null)
                return MSCRMManager.createCase(user.UserNumber, user.CaseProduct, user.CaseTitle, user.CaseDescription, Convert.ToBase64String(user.CaseAttachment), user.CaseFileName, user.CaseFileSize, user.CaseFileType);
            else
                return MSCRMManager.createCase(user.UserNumber, user.CaseProduct, user.CaseTitle, user.CaseDescription, "", user.CaseFileName, user.CaseFileSize, user.CaseFileType);
        }

        private void CreateProductsForUser(Message message, UserInfo user)
        {
            SendMessageToClient(message, "لطفا منتظر بمانید....", RestartKeyboard());
            Bot.SendChatActionAsync(message.From.Id, ChatAction.Typing);
            var products = MSCRMManager.getProductSubjects(user.UserNumber);
            if (products?.Count > 0)
            {
                SendMessageToClient(message, "لطفا نام محصول را از لیست زیر انتخاب فرمایید", CreateProductKeyboard(products));
                user.UserState = UserState.EnterProduct;
            }
            else
            {
                SendMessageToClient(message, "برای این شماره محصولی تعریف نشده است.", RestartKeyboard());
            }
        }

        private ReplyKeyboardMarkup CreateProductKeyboard(List<string> products)
        {

            var followersKeyboard = new KeyboardButton[products.Count() + 1][];
            for (var i = 0; i < products.Count(); i++)
            {
                followersKeyboard[i] = new[]
                {
                    new KeyboardButton(products[i])
                };
            }
            followersKeyboard[products.Count()] = new[]
            {
                new KeyboardButton("شروع دوباره")
            };
            return new ReplyKeyboardMarkup(followersKeyboard);

        }

        private string CorrectPhone(string phone)
        {
            if (phone.StartsWith("98") && phone.Length == 12)
            {
                return "0" + phone.Substring(2);
            }
            else if (phone.StartsWith("+98") && phone.Length == 13)
            {
                return "0" + phone.Substring(3);
            }
            else
            {
                return phone;
            }
        }

        private string GetCaseStatus(UserInfo user)
        {
            var result = MSCRMManager.getCaseStatus(user.UserNumber, user.CaseId);
            return result;
        }

        private ReplyKeyboardMarkup RestartKeyboard()
        {
            var keyboard = new ReplyKeyboardMarkup(new[]
                {
                    new [] // first row
                    {
                        new KeyboardButton("شروع دوباره"),
                    }
                });

            return keyboard;
        }
        private ReplyKeyboardMarkup ContactKeyboard()
        {
            var keyboard = new ReplyKeyboardMarkup(new[]
                {
                new [] // first row
                    {
                        new KeyboardButton("ارسال شماره"){ RequestContact = true },
                    },
                new [] // second row
                    {
                        new KeyboardButton("شروع دوباره"),
                    }
                });

            return keyboard;
        }

        private async void CreateMainMenu(Message message, UserInfo user)
        {
            try
            {
                var keyboard = new ReplyKeyboardMarkup(new[]
                {
                    new [] // first row
                    {
                        new KeyboardButton("پیگیری"),
                        new KeyboardButton("درخواست جدید"),
                    },
                    new [] // Second row
                    {
                        new KeyboardButton("تماس با ما"),
                    }
                });

                SendMessageToClient(message, "به بات خدمت رسان گروه رایانه ای تدبیرپرداز خوش آمدید", keyboard);
            }
            catch (Exception)
            {

                //throw;
            }
        }

        internal async void SendMessageToClient(
                                                Message message,
                                                string messageText,
                                                ReplyKeyboardMarkup key = null,
                                                [CallerMemberName] string methodName = null,
                                                [CallerFilePath] string sourceFile = null,
                                                [CallerLineNumber] int lineNumber = 0,
                                                long targetId = default(long))
        {
            try
            {
                var receiverId = message?.Chat?.Id ?? targetId;

                if (key?.Keyboard != null)
                {
                    if (messageText.Length > 4096 && messageText.Length < 8193)
                    {
                        var part1 = messageText.Substring(0, 4096);
                        var part2 = messageText.Substring(4097);

                        await Bot.SendTextMessageAsync(receiverId, $"1) {Environment.NewLine}" + part1);
                        await Bot.SendTextMessageAsync(receiverId, $"2) {Environment.NewLine} " + part2, replyMarkup: key);
                        return;
                    }
                    if (messageText.Length > 8192)
                    {
                        var part1 = messageText.Substring(0, 4096);
                        var part2 = messageText.Substring(4097, 4096);
                        var part3 = messageText.Substring(8193);

                        await Bot.SendTextMessageAsync(receiverId, $"1) {Environment.NewLine}" + part1);
                        await Bot.SendTextMessageAsync(receiverId, $"2) {Environment.NewLine} " + part2);
                        await Bot.SendTextMessageAsync(receiverId, $"3) {Environment.NewLine} " + part3, replyMarkup: key);
                        return;
                    }

                    await Bot.SendTextMessageAsync(receiverId, messageText, replyMarkup: key);
                }
                else
                {
                    if (messageText.Length > 4096)
                    {
                        var part1 = messageText.Substring(0, 4096);
                        var part2 = messageText.Substring(4097);
                        await Bot.SendTextMessageAsync(receiverId, part1);
                        await Bot.SendTextMessageAsync(receiverId, part2);
                        return;
                    }
                    await Bot.SendTextMessageAsync(receiverId, messageText);
                }
            }

            catch (Exception exception)
            {
                exception.Source = $"{sourceFile} - {methodName} - {lineNumber}";
                try
                {
                    if (exception.Message.ToLower().Contains("block"))
                    {
                        return;
                    }

                    await Bot.SendTextMessageAsync(message?.Chat?.Id ?? 0, "پوزش، دوباره امتحان کنید");
                }
                catch
                {
                }

                //throw;
            }
        }
    }
}