using HomeGPT_Messenger.Models;
using HomeGPT_Messenger.Services;
using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Text.Json;

namespace HomeGPT_Messenger.Pages;

public partial class ImageChatPage : ContentPage
{
	private readonly Guid _chatId;

	private readonly List<Chat> _allChats;
	private Chat Chat => _allChats.First(c => c.Id == _chatId);

	private CancellationTokenSource? _cts;
	private string BuildSdUrl()
	{
		var ip = Preferences.Get("llm_ip", "");

		var port = Preferences.Get("sd_port", 1);

		return $"{ip}:{port}";
	}
	public ImageChatPage(Guid chatId, List<Chat> allChats)
	{
		InitializeComponent();

		_chatId = chatId;

		_allChats = allChats;

		Chat.Kind = ChatKid.Image;

		ChatNameLabel.Text = Chat.Name;

		SizeButton.Text = $"{Chat.SdWidth}x{Chat.SdHeight}";

		StepsButton.Text = $"Steps:{Chat.SdSteps}";

		NegButton.Text = string.IsNullOrWhiteSpace(Chat.SdNegative) ? "N" : "N*";

		foreach (var m in Chat.Messages.OrderBy(m => m.Timestamp))
			AddBubble(m);

		ScrollToBottom();
    }

    #region Menu
    private void OnMenuButtonClicked(object sender, EventArgs e)
	{
		SideMenu.IsVisible=!SideMenu.IsVisible;
	}

	private void OnMenuOverlayTapped(object sender, EventArgs e)
	{
		SideMenu.IsVisible = false;
	}

	private async void OnChatsClicked(object sender, EventArgs e)
	{
		SideMenu.IsVisible=false;

		await Navigation.PushAsync(new ChatsPage());
	}
    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        SideMenu.IsVisible = false;

        await Navigation.PushAsync(new SettingsPage());
    }
    #endregion

    #region Settings
    private async void OnNegClicked(object sender, EventArgs e)
	{
		var v= await DisplayPromptAsync("Negative", "Введите negative:", initialValue: Chat.SdNegative);

		if (v == null) return;

		Chat.SdNegative=string.IsNullOrWhiteSpace(v)?null:v.Trim();

		NegButton.Text = string.IsNullOrWhiteSpace(Chat.SdNegative) ? "N" : "N*";

		await ChatStorageService.SaveChatsAsync(_allChats);
	}

	private async void OnSizeClicked(object sender, EventArgs e)
	{
		var choice = await DisplayActionSheet("Размер", "Отмена", null, "384x512", "512x512", "640x640");

		if (choice == null||choice=="Отмена") return;

		var p=choice.Split('x');

		Chat.SdWidth = int.Parse(p[0]);

		Chat.SdHeight = int.Parse(p[1]);

		SizeButton.Text = choice;

		await ChatStorageService.SaveChatsAsync(_allChats);
	}

	private async void OnStepsClicked(object sender, EventArgs e)
	{
		var v = await DisplayPromptAsync("Steps", "4-8 для sd-turbo", initialValue: Chat.SdSteps.ToString(), keyboard: Keyboard.Numeric);

        if (int.TryParse(v, out var steps)&& steps>0&&steps<=20)
        {
			Chat.SdSteps = steps;

			StepsButton.Text = $"Steps:{steps}";

			await ChatStorageService.SaveChatsAsync(_allChats);
        }
    }
    #endregion

    #region Button
    private void InputEntry_TextChanged(object sender, EventArgs e)
	{
		GenerationButton.IsEnabled=!string.IsNullOrWhiteSpace(InputEntry.Text);
	}
	private async void InputEntry_Completed(object sender, EventArgs e)
	{
		await GenerateAsync();
	}
	private async void OnGenerateClicked(object sender, EventArgs e)
	{
        await GenerateAsync();
    }
	private async void OnCancelClicked(object sender, EventArgs e)
	{
		_cts?.Cancel();//Временно
	}
    #endregion
	
    #region Generation
    private async Task GenerateAsync()
	{
		var promt = (InputEntry.Text ?? "").Trim();

		if (promt.Length == 0) return;

		var userMsg = new Message
		{
			Sender="user",
			Text= promt,
			Timestamp=DateTime.Now,
			Status=MessageStatus.Done
		};

		Chat.Messages.Add(userMsg);

		AddBubble(userMsg);

		InputEntry.Text = "";

		var waitMsg = new Message
		{
			Sender = "LLM",
			Text = "Генерация...",
			Timestamp = DateTime.Now,
			Status = MessageStatus.WaitingForResponse
		};

		Chat.Messages.Add(waitMsg);

		var waitView=AddBubble(waitMsg);

		await ChatStorageService.SaveChatsAsync(_allChats);

		ScrollToBottom();

		_cts?.Cancel();

		_cts=new CancellationTokenSource();

		try
		{
			ChatStatusLabel.Text = "Генерация";

			CancelButton.IsVisible = true;

			GenerationButton.IsEnabled = false;

			using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

			var body = new
			{
				prompt = promt,
				negative_prompt = Chat.SdNegative,
				steps = Chat.SdSteps,
				guidance_scale = 0.0,
				width = Chat.SdWidth,
				height = Chat.SdHeight
			};

			var resp = await http.PostAsJsonAsync($"http://{BuildSdUrl()}/txt2img", body, _cts.Token);

			resp.EnsureSuccessStatusCode();

			var json = await resp.Content.ReadAsStringAsync(_cts.Token);

			var dto = JsonSerializer.Deserialize<Txt2ImgResp>(json);

			if (dto == null || string.IsNullOrWhiteSpace(dto.image_base64))
				throw new Exception("Пустой ответ от SD (API)");

			var bytes = Convert.FromBase64String(dto.image_base64);

			var dir = Path.Combine(FileSystem.AppDataDirectory, "Images");

			Directory.CreateDirectory(dir);

			var path = Path.Combine(dir, $"img_{DateTime.Now:yyyyMMdd_HHmmssfff}.png");

			File.WriteAllBytes(path, bytes);

			waitMsg.Status = MessageStatus.Done;

			waitMsg.Text = "";

			waitMsg.ImagePath = path;

			MessagesLayout.Remove(waitView);

			AddBubble(waitMsg);

			ChatStatusLabel.Text = "Готов";

			await ChatStorageService.SaveChatsAsync(_allChats);

			ScrollToBottom();
		}
		catch (TaskCanceledException)
		{
			waitMsg.Status = MessageStatus.Error;

			waitMsg.Text = "Отменено";

			waitMsg.ImagePath = null;

			MessagesLayout.Remove(waitView);

			AddBubble(waitMsg);

			ChatStatusLabel.Text = "Отменено";

			await ChatStorageService.SaveChatsAsync(_allChats);
		}
		catch (HttpRequestException ex)
		{
			waitMsg.Status=MessageStatus.Error;

			waitMsg.Text="Сеть/SD API недоступен: "+ex.Message;

			waitMsg.ImagePath = null;	

			MessagesLayout.Remove(waitView);

			AddBubble(waitMsg);

			ChatStatusLabel.Text = "Ошибка сети";

			await ChatStorageService.SaveChatsAsync(_allChats);
		}
		catch (Exception ex)
		{
			waitMsg.Status = MessageStatus.Error;

			waitMsg.Text = "Ошибка:" + ex.Message;

			waitMsg.ImagePath = null;

			MessagesLayout.Remove(waitView);

			AddBubble(waitMsg);

			ChatStatusLabel.Text = "Ошибка";

			await ChatStorageService.SaveChatsAsync(_allChats);
		}
		finally
		{
			CancelButton.IsVisible = false;

			GenerationButton.IsEnabled = true;

			_cts = null;
		}
	}
    #endregion

    #region Render
    private View AddBubble(Message m)
	{
        var timeLabel = new Label
        {
            Text = m.Timestamp.ToString("HH:mm"),
            TextColor = Colors.Gray,
            FontSize = 11,
            HorizontalOptions = LayoutOptions.End,
            IsVisible = Preferences.Get("showTime", true)
        };

        View contentView;

		if (!string.IsNullOrEmpty(m.ImagePath) && File.Exists(m.ImagePath))
		{
			contentView = new Image
			{
				Source=ImageSource.FromFile(m.ImagePath),
				Aspect=Aspect.AspectFill,
				HeightRequest=300
			};
		}
		else
		{
			contentView = new Label()
			{
				Text=m.Text,
				TextColor = (Color)Application.Current.Resources["TextColor"],
				FontSize = (double)Application.Current.Resources["AppFontSize"],
				LineBreakMode=LineBreakMode.WordWrap
			};
		}

		var stack = new VerticalStackLayout
		{
			Spacing=6,
			Children = {contentView, timeLabel }
        };

		bool isUser=string.Equals(m.Sender,"user",StringComparison.OrdinalIgnoreCase);

		var frame = new Frame()
		{
			BackgroundColor=isUser
			? (Color)Application.Current.Resources["MessageBubbleUser"]
			: (Color)Application.Current.Resources["MessageBubbleAI"],
			CornerRadius=16,
			Padding=contentView is Image? 8: 10,
			HasShadow=false,
			HorizontalOptions=isUser?LayoutOptions.End:LayoutOptions.Start,
			Margin=isUser?new Thickness(60,6,8,6):new Thickness(8,6,60,6),
			MaximumHeightRequest=700,
			Content=stack
		};

		MessagesLayout.Children.Insert(MessagesLayout.Children.Count-1, frame);

		return frame;
	}
    #endregion

	private void ScrollToBottom()
	{
		MessagesScrollView.ScrollToAsync(ChatBottomAnchor, ScrollToPosition.End, true);
	}

	private sealed class Txt2ImgResp
	{
		public string image_base64 { get; set; } = "";

		public int seed { get; set; }

		public double took_seconds { get; set; }

		public int width { get; set; }

		public int height { get; set; }
	}
}