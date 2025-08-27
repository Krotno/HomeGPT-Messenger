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
        private CancellationTokenSource _requestCts;

        private string BuildOllamaUrl()
        {
            var ip = Preferences.Get("llm_ip", "");

            var port = Preferences.Get("llm_port", 11434);

            return $"http://{ip}:{port}/api/chat";
        }
        public MainPage(Guid chatId,List<Chat> chats)
        {
            InitializeComponent();
            currentChat = chats.FirstOrDefault(c=>c.Id==chatId);
            allChats = chats;
            var globalModel = Preferences.Get("llm_model", "");
            if (string.IsNullOrWhiteSpace(currentChat.ModelName))
            {
                currentChat.ModelName = globalModel;
            }
            RenderMessages();
            NavigationPage.SetHasBackButton(this, false);
            ChatNameLabel.Text = currentChat.Name??"Чат";
            ChatStatusLabel.Text = "Готов";
            SendButton.IsEnabled = false;
            SendButton.Opacity = 0.5;
            //SelectedModel.Text = $"Model:{Preferences.Get("llm_model", "")}";
        }

        #region Animation(Анимации Ассистент)
        private async Task StartTypingStatusAsync()
        {
            try
            {
                _typingStatusCts?.Cancel();
                _typingStatusCts?.Dispose();
                _typingStatusCts = new CancellationTokenSource();

                var cts = _typingStatusCts;
                var token = cts.Token;
                string baseText = "Ассистент генерирует ответ";
                int maxDots = 5; int dots = 0;

                while (!token.IsCancellationRequested && _typingStatusCts == cts)
                {
                    dots = (dots + 1) % (maxDots + 1);
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        if (this.Handler == null || ChatStatusLabel?.Handler == null) return;
                        if (_typingStatusCts != cts || token.IsCancellationRequested) return;
                        ChatStatusLabel.Text = baseText + new string('.', dots);
                    });

                    await Task.Delay(400, token);
                }
            }
            catch (OperationCanceledException) {/*Корректная отмена анимации*/}
            catch (ObjectDisposedException) {/*Ловим страницу*/}
            catch (Exception ex)
            {
                if (this?.Handler != null)
                    await MainThread.InvokeOnMainThreadAsync(() =>
                        DisplayAlert("Ошибка просим обратиться в поддержку", ex.Message, "ОК"));
            }
            finally
            {
                if (this?.Handler != null && ChatStatusLabel?.Handler != null)
                    Device.BeginInvokeOnMainThread(() => ChatStatusLabel.Text = "Готов");
            }
        }

        private void StopTypingStatus()
        {
            _typingStatusCts?.Cancel();
            _typingStatusCts?.Dispose();
            _typingStatusCts = null;
            if (this?.Handler != null && ChatStatusLabel?.Handler != null)
                    Device.BeginInvokeOnMainThread(() => ChatStatusLabel.Text = "Готов");
        }

        protected override void OnDisappearing()
        {
            try
            {
                _typingStatusCts?.Cancel();
                _typingStatusCts?.Dispose();
                _typingStatusCts = null;

                _requestCts?.Cancel();
                _requestCts?.Dispose();
                _requestCts = null;
            }
            catch
            {
                //Ловим ошибку при ответе LLM
            }
            base.OnDisappearing();
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
            CancelButton.IsVisible = true;

            _requestCts?.Cancel();
            _requestCts?.Dispose();
            _requestCts=new CancellationTokenSource();

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
                        var aiText = await SendToLLMAsync(currentChat, allMessages,_requestCts.Token);//отправка копии
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
                            //DisplayAlert("Ошибка просим обратиться в поддержку", "Запрос был отменен", "ОК");
                        });
                        userMessage.Status=MessageStatus.Done;
                        await ChatStorageService.SaveChatsAsync(allChats);
                    }
                    catch (Exception ex)
                    {
                        Device.BeginInvokeOnMainThread(() =>
                        {
                            StopTypingStatus();
                            DisplayAlert("Ошибка просим обратиться в поддержку", ex.Message, "ОК");
                        });
                        userMessage.Status = MessageStatus.Done;
                        await ChatStorageService.SaveChatsAsync(allChats);
                    }
                    finally
                    {
                        Device.BeginInvokeOnMainThread(() =>
                        {
                            isWaiting = false;
                            CancelButton.IsVisible = false;
                            SendButton.IsEnabled = !string.IsNullOrWhiteSpace(InputEntry.Text) && !isWaiting;
                            SendButton.Opacity = SendButton.IsEnabled ? 1.0 : 0.5;
                            _requestCts?.Dispose();
                            _requestCts = null;
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ошибка просим обратиться в поддержку", ex.Message, "ОК");
                isWaiting=false;
                CancelButton.IsVisible = false;
            }            
        }

        private bool isWaiting = false;
        private async Task<string> SendToLLMAsync(Chat chat,List<Message> messages, CancellationToken ct)
        {
            try
            {
                var model = string.IsNullOrWhiteSpace(chat.ModelName) ? Preferences.Get("llm_model", "") : chat.ModelName;
                using var client = new HttpClient(); client.Timeout = TimeSpan.FromMinutes(5);//ждет ответа модели 5 минут
                bool HasSystem = !string.IsNullOrWhiteSpace(chat.SystemPromt); const int limit = 20; int tail = HasSystem ? limit - 1 : limit;

                var lastMsgs= messages.TakeLast(Math.Max(0,tail)).Select(mbox => new//Отправляет 20 сообщений за раз
                {
                    role = mbox.Sender == "user" ? "user" : "assistant",
                    content = mbox.Text
                }).ToList();

                var messagesForLLM = new List<object>(limit);
                if (HasSystem)
                    messagesForLLM.Add(new { role = "system", content = chat.SystemPromt!.Trim() });

                messagesForLLM.AddRange(lastMsgs);
                
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

                var response = await client.PostAsJsonAsync(BuildOllamaUrl(), reqObj,ct);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: ct);
                return json?.message?.content ?? "(No response)";
            }
            catch (TaskCanceledException) { throw; }
            catch (Exception ex) { return $"[Error:{ex.Message}]"; }
        }
        #region (Class) Классы для модели
        public class OllamaResponse
        {
            public MessageObj message { get; set; }
            public class MessageObj
            { 
                public string role { get; set; } 
                public string content { get; set; }
            }
        }

        class OllanaTags
        {
            public List<OllamaModel> models { get; set; }
        }

        class OllamaModel
        {
            public string name { get; set; } = "";
        }
        #endregion

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
                var splitText = Preferences.Get("llm_model","");
                SelectedModel.Text = $"M:{splitText.Split('-', ':')[0]}";
                RenderMessages();
                _ = ScrollMessagesToEndAsync();

                //var lastMsg = currentChat.Messages.LastOrDefault();
                //if (lastMsg != null && lastMsg.Sender == "user" &&
                //    (currentChat.Messages.Count == 1 || currentChat.Messages.Last().Sender == "user"))
                //{
                //    _ = StartTypingStatusAsync();
                //}
                //else
                //{
                //    StopTypingStatus();
                //}

                var last = currentChat.Messages.LastOrDefault();
                var wainting = last?.Sender == "user" && last?.Status == MessageStatus.WaitingForResponse;

                if (wainting) _ = StartTypingStatusAsync();
                else StopTypingStatus();

                CancelButton.IsVisible=wainting;
                isWaiting = wainting;
                SendButton.IsEnabled=!wainting&& !string.IsNullOrWhiteSpace(InputEntry.Text);
                SendButton.Opacity = SendButton.IsEnabled ? 1.0 : 0.5;
                UpdatePromtBadge();
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
            string[] models;
            try
            {
                models = await GetOllamaModelsAsync();
                if (models.Length == 0) models = new[] { "llama3", "mistral:7b", "tinyllama:1.1b" };
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ollama", $"Не удалось получить список моделей:\n{ex.Message}", "Ок");
                models=new[] { "llama3", "mistral:7b", "tinyllama:1.1b" };
            }

            var selected = await DisplayActionSheet("Выберите модель", "Отмена", null,models);

            if (string.IsNullOrEmpty(selected) || selected == "Отмена") return;

            currentChat.ModelName = selected;

            await ChatStorageService.SaveChatsAsync(allChats);

            Preferences.Set("llm_model", selected);

            SelectedModel.Text = $"M:{selected.Split('-', ':')[0]}";
        }

        private async Task<string[]> GetOllamaModelsAsync()
        {
            var ip = Preferences.Get("llm_ip", "");
            var baseUrl = $"http://{ip}";
            using var http = new HttpClient{BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(5)};
            var resp = await http.GetAsync("/api/tags");
            resp.EnsureSuccessStatusCode();
            var data = await resp.Content.ReadFromJsonAsync<OllanaTags>();
            return (data?.models ?? new()).Select(m => m.name).OrderBy(n => n).ToArray();
        }

        private void OnCancelClicked(object sender, EventArgs e)
        {
            _requestCts?.Cancel();
            StopTypingStatus();
            isWaiting = false;
            CancelButton.IsVisible=false;
            SendButton.IsEnabled=!string.IsNullOrWhiteSpace(InputEntry.Text);
            SendButton.Opacity = SendButton.IsEnabled ? 1.0 : 0.5;
        }
        #region Promt
        private async void OnPromtClicked(object sender, EventArgs e)
        {
            var current = currentChat.SystemPromt ?? "";
            var result = await DisplayPromptAsync(
                "Системный промт",
                "Короткий контекст для модели (до 100 символов). Пусто = без промта",
                accept: "Сохранить",
                cancel: "Отмена",
                placeholder: "Напри.: «Отвечай кратко, на русском»)",
                maxLength: 100,
                keyboard: Keyboard.Default,
                initialValue: current
                );

            if (result is null) return;
            result = result.Trim();
            if (result.Length > 100) result = result[..100];
            
            currentChat.SystemPromt= result;
            await ChatStorageService.SaveChatsAsync(allChats);
            await DisplayAlert("Промт", string.IsNullOrEmpty(result) ? "Промт очищен" : "Промт сохранён", "Ок");
            PromtButton.Text = !string.IsNullOrWhiteSpace(currentChat.SystemPromt) ? "P*" : "P";
        }

        private void UpdatePromtBadge()
        {
            var Has = !string.IsNullOrWhiteSpace(currentChat.SystemPromt);
            PromtButton.Text = Has ? "P*" : "P";
            AutomationProperties.SetHelpText(PromtButton, Has ? "Задан системный промт" : "Системный промт не задан");
        }
        #endregion

    }
}