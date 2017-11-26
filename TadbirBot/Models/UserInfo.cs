using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TadbirBot.Models
{
    public class UserInfo
    {
        public long TelegramId { get; set; }
        public string TelegramName { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string CaseId { get; set; }
        public string UserNumber { get; set; }
        public string Email { get; set; }
        public string CaseDescription { get; set; }
        public string CaseStatus { get; set; }
        public string CaseProduct { get; set; }
        public string CaseTitle { get; set; }
        public byte[] CaseAttachment { get; set; }
        public int CaseFileSize { get; set; }
        public string CaseFileName { get; set; }
        public string CaseFileType { get; set; }
        public UserState UserState { get; set; }
    }

    public class Tfile
    {
        public string FileId { get; set; }
        public int FileSize { get; set; }
        public string FilePath { get; set; }
    }

    public class FileDownloadResult : Tfile
    {
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public byte[] Buffer { get; set; }
    }
}