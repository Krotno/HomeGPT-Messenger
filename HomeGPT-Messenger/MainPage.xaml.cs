using HomeGPT_Messenger.Models;
using System.Net.Http.Json;


namespace HomeGPT_Messenger
{
    public partial class MainPage : ContentPage
    {
        private List<Message> Messages = new();

        private const string OLLAMA_URL = "http://192.168.3.77:11434/api/chat";

        public MainPage()
        {
            InitializeComponent();

            
        }

        private async void OnSendClicked(object sender, EventArgs e)
        {
            var userText = InputEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(userText)) return;

            // ADD message user
            var userMessage = new Message { Sender = "user", Text = userText, Timestamp = DateTime.Now };
            Messages.Add(userMessage);
            InputEntry.Text = string.Empty;
            RenderMessages();

            // ОТПРАВЛЯЕМ К LLM
            var aiText = await SendToLLMAsync(userText);
            var aiMessage = new Message { Sender = "ai", Text = aiText, Timestamp = DateTime.Now };
            Messages.Add(aiMessage);
            RenderMessages();
        }

        private async Task<string> SendToLLMAsync(string prompt)
        {
            try
            {
                using var client = new HttpClient();
                var reqObj = new
                {
                    model = "dolphin-mistral",
                    stream=false,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    }
                };
                var response = await client.PostAsJsonAsync(OLLAMA_URL, reqObj);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadFromJsonAsync<OllamaResponse>();
                return json?.message?.content ?? "(No response)";
            }
            catch (Exception ex) { return $"[Error:{ex.Message}]"; }
        }

        //
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
            foreach(var msg in Messages)
            {
                var frame = new Frame
                {
                    BackgroundColor = msg.Sender == "user"
                    ? (Color)Application.Current.Resources["MessageBubbleUser"]
                    : (Color)Application.Current.Resources["MessageBubbleAI"],
                    CornerRadius = 16,
                    Padding = 10,
                    Margin = new Thickness(0, 0, 60, 0),
                    HasShadow = false,
                    HorizontalOptions = msg.Sender == "user" ? LayoutOptions.End : LayoutOptions.Start,
                    Content = new Label
                    {
                        Text = msg.Text,
                        TextColor = (Color)Application.Current.Resources["TextColor"]
                    }
                };
                MessagesLayout.Children.Add(frame);
            }
        }
    }

}
