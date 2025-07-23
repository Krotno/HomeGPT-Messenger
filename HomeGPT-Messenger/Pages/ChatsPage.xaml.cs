using HomeGPT_Messenger.Models;
using HomeGPT_Messenger.Services;

namespace HomeGPT_Messenger.Pages;

public partial class ChatsPage : ContentPage
{
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
			string action = await DisplayActionSheet(
				$"Действия для \"{chat.Name}\"", "Отмена", null, "Переименовать", "Удалить");

			if (action == "Переименовать")
			{
				string newName = await DisplayPromptAsync("Переименовать чат","Новое имя чата:", initialValue: chat.Name);
				if (!string.IsNullOrWhiteSpace(newName))
				{
					chat.Name = newName;
					await ChatStorageService.SaveChatsAsync(chats);
					ChatsList.ItemsSource = null;//Для корректного обновления
					ChatsList.ItemsSource = chats;
				}
			}
			else if(action=="Удалить")
			{
				bool confirm= await DisplayAlert("Удалить чат", $"Удалить \"{chat.Name}\"?", "Да", "Нет");
				if (confirm)
				{
					chats.Remove(chat);
					await ChatStorageService.SaveChatsAsync(chats);
					ChatsList.ItemsSource= null;
					ChatsList.ItemsSource = chats;
				}
			}
		}
	}
}