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
	}

	private async void LoadChats()
	{
		chats=await ChatStorageService.LoadChatsAsync();
        ChatsList.ItemsSource=chats;
	}

	private async void OnAddChatClicked(object sender,EventArgs e)//test
	{
		var chat = new Chat {Name = "Новый чат " +DateTime.Now.ToShortDateString()};
		chats.Add(chat);
		await ChatStorageService.SaveChatsAsync(chats);
        ChatsList.ItemsSource = null;
        ChatsList.ItemsSource = chats;
    }

	private async void OnChatSelected(object sender, SelectionChangedEventArgs e)
	{
		var selected = e.CurrentSelection.FirstOrDefault() as Chat;
		if (selected == null) return;

		await Navigation.PushAsync(new MainPage(selected, chats)); // Переход + передача
	}

	private async void OnChatMenuClicked(object sender, EventArgs e)
	{
		if (sender is Button btn && btn.CommandParameter is Chat chat)
		{
			chatForMenu = chat;//для какого чата меню
			ChatMenuPopup.IsVisible = true;// открытие окна

			//string action = await DisplayActionSheet( //переход на другое окно
			//	$"Действия для \"{chat.Name}\"", "Отмена", null, "Переименовать", "Удалить");

			//if (action == "Переименовать")
			//{
			//	string newName = await DisplayPromptAsync("Переименовать чат","Новое имя чата:", initialValue: chat.Name);
			//	if (!string.IsNullOrWhiteSpace(newName))
			//	{
			//		chat.Name = newName;
			//		await ChatStorageService.SaveChatsAsync(chats);
			//		ChatsList.ItemsSource = null;//Для корректного обновления
			//		ChatsList.ItemsSource = chats;
			//	}
			//}
			//else if(action=="Удалить")
			//{
			//	bool confirm= await DisplayAlert("Удалить чат", $"Удалить \"{chat.Name}\"?", "Да", "Нет");
			//	if (confirm)
			//	{
			//		chats.Remove(chat);
			//		await ChatStorageService.SaveChatsAsync(chats);
			//		ChatsList.ItemsSource= null;
			//		ChatsList.ItemsSource = chats;
			//	}
			//}
		}
	}

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
			chats.Remove(chatForMenu);
			await ChatStorageService.SaveChatsAsync(chats);
			ChatsList.ItemsSource = null;
			ChatsList.ItemsSource = chats;
		}
	}
    private async void Popup_CancelPressed(Object sender, EventArgs e)
    {
        ChatMenuPopup.IsVisible = false;
    }
}