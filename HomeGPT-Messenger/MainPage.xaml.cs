using HomeGPT_Messenger.Models;
using HomeGPT_Messenger.Pages;
using HomeGPT_Messenger.Services;
using System.Net.Http.Json;


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

        private const string OLLAMA_URL = "http://192.168.3.77:11434/api/chat";

        public MainPage(Chat chat,List<Chat> chats)
        {
            InitializeComponent();
            currentChat = chat;
            allChats = chats;
            RenderMessages();
            NavigationPage.SetHasBackButton(this, false);
            ChatNameLabel.Text = currentChat.Name;
            ChatStatusLabel.Text = "Готов";
        }

        private void InputEntry_Completed(object sender, EventArgs e)
        {
            OnSendClicked(sender, e);
        }

        private async void OnSendClicked(object sender, EventArgs e)
        {
            ChatStatusLabel.Text = "Ожидайте ответа.....";
            var userText = InputEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(userText)) return;

            // ADD message user
            var userMessage = new Message { Sender = "user", Text = userText, Timestamp = DateTime.Now };
            currentChat.Messages.Add(userMessage);
            InputEntry.Text = string.Empty;
            RenderMessages();

            // ОТПРАВЛЯЕМ К LLM
            var aiText = await SendToLLMAsync(currentChat);
            var aiMessage = new Message { Sender = "ai", Text = aiText, Timestamp = DateTime.Now };
            currentChat.Messages.Add(aiMessage);
            RenderMessages();

            await ChatStorageService.SaveChatsAsync(allChats);
        }

        private bool isWaiting = false;
        private async Task<string> SendToLLMAsync(Chat chat)
        {
            if (isWaiting) return string.Empty;
            isWaiting = true;

            SendButton.IsEnabled=false;
            SendButton.Opacity = 0.5;
            try
            {
                try
                {
                    using var client = new HttpClient();
                    client.Timeout = TimeSpan.FromMinutes(5);//ждет ответа моедли 5 минут
                    var messagesForLLM = chat.Messages.Select(mbox => new
                    {
                        role = mbox.Sender == "user" ? "user" : "assistant",
                        Content = mbox.Text
                    }).ToList();

                    //currentChat.Messages.TakeLast(20).Select(mbox => new // В дальнейшем для ограничения количества запросов на 20

                    var reqObj = new
                    {
                        model = "dolphin-mistral",
                        stream = false,
                        messages = messagesForLLM
                    };
                    var response = await client.PostAsJsonAsync(OLLAMA_URL, reqObj);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadFromJsonAsync<OllamaResponse>();
                    return json?.message?.content ?? "(No response)";
                }
                catch (Exception ex) { return $"[Error:{ex.Message}]"; }
            }
            finally
            {
                SendButton.IsEnabled = true;
                SendButton.Opacity = 1;
                isWaiting=false;
                ChatStatusLabel.Text = "Готов";
            }
           
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
            foreach(var msg in currentChat.Messages)
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
                    Margin = new Thickness(10,5),
                    HasShadow = false,
                    HorizontalOptions = msg.Sender == "user" ? LayoutOptions.End : LayoutOptions.Start,
                    Content =stack,
                    MaximumWidthRequest=700
                };
                MessagesLayout.Children.Add(frame);
            }
        }
        #region Overlay (для меню)
        private void OnMenuOverlayTapped(object sender, EventArgs e)
        {
            SideMenu.IsVisible = false;
            MenuOverlay.IsVisible = false;
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
