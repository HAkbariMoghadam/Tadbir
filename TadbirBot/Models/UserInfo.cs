﻿using System;
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
		public long CaseId { get; set; }
		public string UserNumber { get; set; }
		public string Email { get; set; }
		public string CaseDescription { get; set; }
		public string CaseStatus { get; set; }
		public CaseType CaseType { get; set; }
		public UserState UserState { get; set; }
	}
}