using HomeGPT_Messenger.Models;
using HomeGPT_Messenger.Pages;
using HomeGPT_Messenger.Services;
using System.Net.Http.Json;
using System.Text;
using System.Diagnostics;
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

        //private const string OLLAMA_URL = "http://192.168.3.77:11434/api/chat";

        private string OLLAMA_URL = "http://" +Preferences.Get("llm_ip","")+ "/api/chat";// "http://192.168.3.77:11434/api/chat";

        public MainPage(Guid chatId,List<Chat> chats)
        {
            InitializeComponent();
            currentChat = chats.FirstOrDefault(c=>c.Id==chatId);
            allChats = chats;
            RenderMessages();
            NavigationPage.SetHasBackButton(this, false);
            ChatNameLabel.Text = currentChat.Name??"Чат";
            ChatStatusLabel.Text = "Готов";
            SendButton.IsEnabled = false;
            SendButton.Opacity = 0.5;
            SelectedModel.Text = $"Model:{Preferences.Get("llm_model", "")}";
        }

        #region Animation(Анимации Ассистент)
        private async Task StartTypingStatusAsync()
        {
            try
            {
                _typingStatusCts?.Cancel();
                _typingStatusCts = new CancellationTokenSource();
                var token = _typingStatusCts.Token;
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
            catch(TaskCanceledException)
            {
                //Ловим ошибку при ответе LLM 
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка просим обратиться в поддержку", ex.Message, "ОК");
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
                {
                    Sender = "user",
                    Text = userText,
                    Timestamp = DateTime.Now,
                    Status = MessageStatus.WaitingForResponse//Прикрутка в будущем статуса
                };//Собирает сообщение пользователя
                currentChat.Messages.Add(userMessage);
                await ChatStorageService.SaveChatsAsync(allChats);
                RenderMessages();
                InputEntry.Text = string.Empty;
                await ScrollMessagesToEndAsync();//листает вниз                
                _ = StartTypingStatusAsync();
                InputEntry.Focus();

                _ = Task.Run(async () =>
                {
                    try
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
                    }
                    catch (TaskCanceledException)
                    {
                        Device.BeginInvokeOnMainThread(() =>
                        {
                            StopTypingStatus();
                            DisplayAlert("Ошибка просим обратиться в поддержку", "Запрос был отменен", "ОК");
                        });
                    }
                    catch (Exception ex)
                    {
                        Device.BeginInvokeOnMainThread(() =>
                        {
                            StopTypingStatus();
                            DisplayAlert("Ошибка просим обратиться в поддержку", ex.Message, "ОК");
                        });
                    }
                    finally
                    {
                        Device.BeginInvokeOnMainThread(() =>
                        {
                            isWaiting = false;
                            SendButton.IsEnabled = !string.IsNullOrWhiteSpace(InputEntry.Text) && !isWaiting;
                            SendButton.Opacity = SendButton.IsEnabled ? 1.0 : 0.5;
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка просим обратиться в поддержку", ex.Message, "ОК");
            }            
        }

        private bool isWaiting = false;
        private async Task<string> SendToLLMAsync(Chat chat,List<Message> messages)
        {
            try
            {
                var model = Preferences.Get("llm_model", "");
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(5);//ждет ответа модели 5 минут

                var messagesForLLM = messages.TakeLast(20).Select(mbox => new//Отправляет 20 сообщений за раз
                {
                    role = mbox.Sender == "user" ? "user" : "assistant",
                    content = mbox.Text
                }).ToList();

                var reqObj = new
                {
                    model,
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

        private async Task RenderMessages()
        {
            try
            {
                MessagesLayout.Children.Clear();
                int i = 0;
                foreach (var msg in currentChat.Messages)
                {
                    try
                    {
                        Debug.WriteLine($"Рендерим сообщение #{i}: {msg.Text}");

                        // Логируем ресурсы и preferences перед использованием
                        if (!Application.Current.Resources.TryGetValue("TextColor", out var textColorObj))
                            Debug.WriteLine("TextColor не найден в Resources");
                        if (!Application.Current.Resources.TryGetValue("AppFontSize", out var appFontSizeObj))
                            Debug.WriteLine("AppFontSize не найден в Resources");
                        if (!Application.Current.Resources.TryGetValue("MessageBubbleUser", out var userBubble))
                            Debug.WriteLine("MessageBubbleUser не найден в Resources");
                        if (!Application.Current.Resources.TryGetValue("MessageBubbleAI", out var aiBubble))
                            Debug.WriteLine("MessageBubbleAI не найден в Resources");

                        var textLabel = new Label
                        {
                            Text = msg.Text,
                            TextColor = (Color)Application.Current.Resources["TextColor"],
                            FontSize = (double)Application.Current.Resources["AppFontSize"]
                        };

                        var timeLabel = new Label
                        {
                            Text = msg.Timestamp.ToString("HH:mm"),
                            TextColor = Colors.Gray,
                            FontSize = 11,
                            HorizontalOptions = LayoutOptions.End,
                            IsVisible = Preferences.Get("showTime", true)
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
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка при добавлении сообщения: {ex.Message} ({msg.Text})");
                        await Application.Current.MainPage.DisplayAlert("Ошибка", $"Ошибка при добавлении сообщения: {ex.Message}", "OK");
                    }
                }
                MessagesLayout.Children.Add(ChatBottomAnchor);
            }
            catch(Exception ex)
            {
                await DisplayAlert("Ошибка просим обратиться в поддержку",ex.Message,"ОК");
            }
        }

        #region InputEntry (Проверка пустоты)
        private void InputEntry_TextChanged(object sender, EventArgs e)
        {
            SendButton.IsEnabled = !string.IsNullOrWhiteSpace(InputEntry.Text) && !isWaiting;
            SendButton.Opacity = SendButton.IsEnabled ? 1.0 : 0.5;
        }
        #endregion

        #region Appearing ( Scroll Листает вниз)+ ChatStorage+ Status Chat
        protected override async void OnAppearing()
        {
            try
            {
                base.OnAppearing();
                allChats = await ChatStorageService.LoadChatsAsync();
                var updatedChat = allChats.FirstOrDefault(c => c.Id == currentChat.Id);
                if (updatedChat != null) currentChat = updatedChat;
                RenderMessages();
                _ = ScrollMessagesToEndAsync();

                var lastMsg = currentChat.Messages.LastOrDefault();
                if (lastMsg != null && lastMsg.Sender == "user" &&
                    (currentChat.Messages.Count == 1 || currentChat.Messages.Last().Sender == "user"))
                {
                    _ = StartTypingStatusAsync();
                }
                else
                {
                    StopTypingStatus();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка просим обратиться в поддержку", ex.Message, "ОК");
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
                    catch(Exception ex) 
                    { 
                        await DisplayAlert("Ошибка просим обратиться в поддержку", ex.Message, "ОК"); 
                    }
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

        private async void SelectedModelButtonClicked(object sender, EventArgs e)
        {
            var models = new[] { "llama3", "dolphin-mistral", "tinyllama:1.1b" };

            string selected = await DisplayActionSheet("Выберите модель", "Отмена", null,models);

            if (string.IsNullOrEmpty(selected) || selected == "Отмена") return;

            Preferences.Set("llm_model", selected);

            SelectedModel.Text = $"Model:{selected}";
        }
    }
}