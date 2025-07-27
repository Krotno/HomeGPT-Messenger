using HomeGPT_Messenger.Models;
using HomeGPT_Messenger.Pages;
using HomeGPT_Messenger.Services;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Maui.Controls;
using System.Threading.Tasks;


namespace HomeGPT_Messenger
{
    public partial class MainPage : ContentPage
    {
        private List<Message> Messages = new();
        /// <summary>
        /// Отвечает за передачу переписки
        /// </summary>
        private Chat currentChat;
        private List<Chat> allChats;
        private CancellationTokenSource _typingStatusCts;//для удобной работы анимации

        private const string OLLAMA_URL = "http://192.168.3.77:11434/api/chat";

        public MainPage(Guid chatId,List<Chat> chats)
        {
            InitializeComponent();
            currentChat = chats.FirstOrDefault(c=>c.Id==chatId);
            allChats = chats;
            RenderMessages();
            NavigationPage.SetHasBackButton(this, false);
            ChatNameLabel.Text = currentChat.Name??"Чат";
            ChatStatusLabel.Text = "Готов";
            
        }

        #region Animation(Анимации Ассистент)
        private async Task StartTypingStatusAsync()
        {
            _typingStatusCts?.Cancel();
            _typingStatusCts=new CancellationTokenSource();
            var token=_typingStatusCts.Token;
            string baseText = "Ассистент генерирует ответ";
            int maxDots = 5;
            int dots = 0;
            while (!token.IsCancellationRequested)
            {
                dots = (dots + 1) % (maxDots + 1);
                Device.BeginInvokeOnMainThread(() =>
                {
                    ChatStatusLabel.Text = baseText + new string('.', dots);
                });                
                await Task.Delay(400, token);
            }
        }

        private void StopTypingStatus()
        {
            _typingStatusCts?.Cancel();
            ChatStatusLabel.Text = "Готов";
        }
        #endregion

        private void InputEntry_Completed(object sender, EventArgs e)
        {
            OnSendClicked(sender, e);
        }

        private async void OnSendClicked(object sender, EventArgs e)
        {
            if(isWaiting) return;
            isWaiting=true;
            SendButton.IsEnabled = false;
            SendButton.Opacity = 0.5;
            try
            {
                var userText = InputEntry.Text?.Trim();
                if (string.IsNullOrWhiteSpace(userText))
                {
                    InputEntry.Text = string.Empty;
                    StopTypingStatus();
                    return;
                }

                var userMessage = new Message 
                { Sender = "user",
                  Text = userText,
                  Timestamp = DateTime.Now,
                  Status = MessageStatus.WaitingForResponse//Прикрутка в будущем статуса
                };//Собирает сообщение пользователя
                currentChat.Messages.Add(userMessage);
                await ChatStorageService.SaveChatsAsync(allChats);
                RenderMessages(); 
                InputEntry.Text = string.Empty;
                await ScrollMessagesToEndAsync();//листает вниз                
                _=StartTypingStatusAsync();
                InputEntry.Focus();

                _ = Task.Run(async () =>
                {
                    var allMessages = currentChat.Messages.ToList();//Копирует историю + текущее сообщение
                    var aiText = await SendToLLMAsync(currentChat, allMessages);//отправка копии
                    var aiMessage = new Message
                    {
                        Sender = "ai",
                        Text = aiText,
                        Timestamp = DateTime.Now,
                        Status = MessageStatus.Done
                    };//добавление aiMessage в историю
                    currentChat.Messages.Add(aiMessage);

                    userMessage.Status = MessageStatus.Done;

                    await ChatStorageService.SaveChatsAsync(allChats);

                    Device.BeginInvokeOnMainThread(() =>
                    {
                        RenderMessages();
                        ScrollMessagesToEndAsync();// листает вниз
                        StopTypingStatus();
                    });
                });
            }
            finally
            {
                SendButton.IsEnabled = !string.IsNullOrWhiteSpace(InputEntry.Text);
                SendButton.Opacity = SendButton.IsEnabled ? 1.0 : 0.5;
                isWaiting = false;
            }
            
        }

        private bool isWaiting = false;
        private async Task<string> SendToLLMAsync(Chat chat,List<Message> messages)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(5);//ждет ответа модели 5 минут

                var messagesForLLM = messages.Select(mbox => new
                {
                    role = mbox.Sender == "user" ? "user" : "assistant",
                    content = mbox.Text
                }).ToList();

                //currentChat.Messages.TakeLast(20).Select(mbox => new // В дальнейшем для ограничения количества запросов на 20

                var reqObj = new
                {
                    model = "dolphin-mistral",
                    stream = false,
                    messages = messagesForLLM
                };

                //var jsonReq = JsonSerializer.Serialize(reqObj, new JsonSerializerOptions()//тест уходящей инфы
                //{
                //    WriteIndented = true,
                //    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                //});
                //await DisplayAlert("Отправляймые", jsonReq, "OK");

                var response = await client.PostAsJsonAsync(OLLAMA_URL, reqObj);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadFromJsonAsync<OllamaResponse>();
                return json?.message?.content ?? "(No response)";
            }
            catch (Exception ex) { return $"[Error:{ex.Message}]"; }
        }

        public class OllamaResponse
        {
            public MessageObj message { get; set; }
            public class MessageObj
            { 
                public string role { get; set; } 
                public string content { get; set; }
            }
        }

        private void RenderMessages()
        {
            MessagesLayout.Children.Clear();
            foreach (var msg in currentChat.Messages)
            {
                var textLabel = new Label
                {
                    Text = msg.Text,
                    TextColor = (Color)Application.Current.Resources["TextColor"],
                    FontSize = 16
                };

                var timeLabel = new Label
                {
                    Text = msg.Timestamp.ToString("HH:mm"),
                    TextColor = Colors.Gray,
                    FontSize = 10,
                    HorizontalOptions = LayoutOptions.End
                };

                var stack = new VerticalStackLayout
                {
                    Spacing = 2,
                    Children = { textLabel, timeLabel }
                };

                var frame = new Frame
                {
                    BackgroundColor = msg.Sender == "user"
                    ? (Color)Application.Current.Resources["MessageBubbleUser"]
                    : (Color)Application.Current.Resources["MessageBubbleAI"],
                    CornerRadius = 16,
                    Padding = 10,
                    Margin = new Thickness(10, 5),
                    HasShadow = false,
                    HorizontalOptions = msg.Sender == "user" ? LayoutOptions.End : LayoutOptions.Start,
                    Content = stack,
                    MaximumWidthRequest = 700
                };
                MessagesLayout.Children.Add(frame);
            }
            MessagesLayout.Children.Add(ChatBottomAnchor);
        }

        #region InputEntry (Проверка пустоты)
        private void InputEntry_TextChanged(object sender, EventArgs e)
        {
            if (isWaiting) return;
            SendButton.IsEnabled=!string.IsNullOrWhiteSpace(InputEntry.Text);
            SendButton.Opacity = SendButton.IsEnabled ? 1.0 : 0.5;
        }
        #endregion

        #region Appearing ( Scroll Листает вниз)+ ChatStorage+ Status Chat
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            allChats = await ChatStorageService.LoadChatsAsync();
            var updatedChat= allChats.FirstOrDefault(c=>c.Id==currentChat.Id);
            if (updatedChat != null) currentChat = updatedChat;
            RenderMessages();
            _ = ScrollMessagesToEndAsync();

            var lastMsg = currentChat.Messages.LastOrDefault();
            if (lastMsg != null && lastMsg.Sender == "user" &&
                (currentChat.Messages.Count == 1 || currentChat.Messages.Last().Sender == "user"))
            {
                _=StartTypingStatusAsync();
            }
            else
            {
                StopTypingStatus();
            }
        }

        private async Task ScrollMessagesToEndAsync()
        {
            Device.BeginInvokeOnMainThread(async() =>
            {
                await Task.Delay(100);
                if (MessagesLayout.Children.Contains(ChatBottomAnchor))
                {
                    try
                    {
                        MessagesScrollView.ScrollToAsync(ChatBottomAnchor, ScrollToPosition.End, true);
                    }
                    catch { }
                }
            });
        }
        #endregion

        #region Overlay (Для меню) 
        private void OnMenuOverlayTapped(object sender, EventArgs e)
        {
            SideMenu.IsVisible = false;
        }
        #endregion

        #region ButtonMenu (Кнопки меню)
        private void OnMenuButtonClicked(object sender, EventArgs e)
        {
            SideMenu.IsVisible = !SideMenu.IsVisible;
        }

        private async void OnChatsClicked(object sender, EventArgs e)
        {
            SideMenu.IsVisible = false;
            await Navigation.PushAsync(new ChatsPage());

        }

        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            SideMenu.IsVisible = false;
            await Navigation.PushAsync(new SettingsPage());
        }
        #endregion
    }
}