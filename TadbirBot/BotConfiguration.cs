using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Web;
using TadbirBot.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TadbirBot
{
	public class BotConfiguration
	{
		internal static readonly TelegramBotClient Bot = new TelegramBotClient(ConfigurationManager.AppSettings["BotApiToken"]);
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
			//throw new NotImplementedException();
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
						SendMessageToClient(message, "لطفا شماره پیگیری درخواست خود را به صورت عددی و صحیح وارد کنید.", RestartKeyboard());
						user.UserState = UserState.EnterCaseNumber;
						break;
					case UserState.EnterCaseNumber:
						if (long.TryParse(message.Text, out long caseId))
						{
							user.CaseId = caseId;
							user.UserState = UserState.EnterContactForStatus;
							SendMessageToClient(message, "لطفا شماره تلفنی که با آن درخواست را ثبت کرده اید ارائه دهید.", ContactKeyboard());
						}
						else
						{
							SendMessageToClient(message, "لطفا شماره پیگیری درخواست خود را به صورت عددی و صحیح وارد کنید.", RestartKeyboard());
						}
						break;
					case UserState.EnterContactForStatus:
						user.UserNumber = message.Contact.PhoneNumber;
						SendMessageToClient(message, "لطفا منتظر بمانید....", RestartKeyboard());
						Bot.SendChatActionAsync(message.From.Id, ChatAction.Typing);
						user.CaseStatus = GetCaseStatus(user);
						SendMessageToClient(message, "درخواست شما در وضعیت زیر است.", RestartKeyboard());
						SendMessageToClient(message, user.CaseStatus, RestartKeyboard());
						break;
					case UserState.NewCase:
						break;
					case UserState.EnterContactForNew:
						break;
					case UserState.EnterCaseType:
						break;
					case UserState.EnterCaseDescription:
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
			catch (Exception)
			{
				result = false;
				//throw;
			}
			return result;
		}

		private string GetCaseStatus(UserInfo user)
		{
			return "Status From CRM";
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

				SendMessageToClient(message, "به بات خدمت رسان تدبیر خوش آمدید", keyboard);
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