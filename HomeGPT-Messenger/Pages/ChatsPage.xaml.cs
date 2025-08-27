using HomeGPT_Messenger.Models;
using HomeGPT_Messenger.Services;
using System;

namespace HomeGPT_Messenger.Pages;

public partial class ChatsPage : ContentPage
{
    private Chat chatForMenu;//Для корректной работы окна показа удаления/переименования.

    private List<Chat> chats = new();
	public ChatsPage()
	{
		InitializeComponent();
		LoadChats();
        NavigationPage.SetHasBackButton(this, false);
    }

	private async void LoadChats()
	{
		chats=await ChatStorageService.LoadChatsAsync();
        ChatsList.ItemsSource=chats;
	}

	private async void OnAddChatClicked(object sender,EventArgs e)//test
	{
		var type = await DisplayActionSheet("Тип чата", "Отмена", null, "Текст", "Изображения");
		if (type is null || type == "Отмена") return;

		string name = await DisplayPromptAsync("Новый чат", "Введите название чата: ");
		if (string.IsNullOrWhiteSpace(name)) return;

		var chat=new Chat 
		{ 
			Name= name.Trim(),
			Kind=type=="Текст" ? ChatKid.Text: ChatKid.Image
		};

		chats.Add(chat);
		await ChatStorageService.SaveChatsAsync(chats);
        ChatsList.ItemsSource = null;
        ChatsList.ItemsSource = chats;
    }

	private async void OnChatMenuClicked(object sender, EventArgs e)
	{
		if (sender is Button btn && btn.CommandParameter is Chat chat)
		{
			chatForMenu = chat;//для какого чата меню
			ChatMenuPopup.IsVisible = true;// открытие окна
		}
	}
    #region Overlay (для меню)
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
    #region PopupMenu (модули удаление и Rename)
    private async void Popup_RenamePressed(Object sender, EventArgs e)
	{
		ChatMenuPopup.IsVisible = false;
        string newName = await DisplayPromptAsync("Переименовать чат", "Новое имя чата:", initialValue: chatForMenu.Name);
		if (!string.IsNullOrWhiteSpace(newName))
		{
			chatForMenu.Name = newName;
			await ChatStorageService.SaveChatsAsync(chats);
			ChatsList.ItemsSource = null;//Для корректного обновления
			ChatsList.ItemsSource = chats;
		}
	}
    private async void Popup_DeletePressed(Object sender, EventArgs e)
    {
        ChatMenuPopup.IsVisible = false;
        bool confirm = await DisplayAlert("Удалить чат", $"Удалить \"{chatForMenu.Name}\"?", "Да", "Нет");
		if (confirm)
		{
			var mediaToCheck = chatForMenu.Messages.Where(m => !string.IsNullOrEmpty(m.ImagePath))
			.Select(m=> m.ImagePath!).ToList();

			chats.Remove(chatForMenu);
			await ChatStorageService.SaveChatsAsync(chats);

			CleanupUnusedMedia(mediaToCheck, chats);

			ChatsList.ItemsSource= null;
			ChatsList.ItemsSource = chats;
        }
	}

	private void CleanupUnusedMedia(List<string> paths, List<Chat> allChats)
	{
		foreach (var path in paths)
		{
			bool usedSomewhereElse=allChats.SelectMany(c=>c.Messages).Any(m=>m.ImagePath==path);
			
			if (!usedSomewhereElse && File.Exists(path))
			{
				try
				{
					File.Delete(path);
					System.Diagnostics.Debug.WriteLine($"[CLEANUP] Удален файл {path}");
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"[CLEANUP ERROR] {ex.Message}");
				}
			}
		}
	}
    private async void Popup_CancelPressed(Object sender, EventArgs e)
    {
        ChatMenuPopup.IsVisible = false;
    }
	#endregion

	private async void OnChatFrameTapped(object sender, EventArgs e)
	{
        if (sender is Frame frame && frame.BindingContext is Chat chat)
        {
			if (chat.Kind == ChatKid.Text) await Navigation.PushAsync(new MainPage(chat.Id, chats));
			else await Navigation.PushAsync(new ImageChatPage(chat.Id, chats));
        }
    }
}