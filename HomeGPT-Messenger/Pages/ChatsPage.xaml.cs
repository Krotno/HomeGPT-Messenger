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

		await Navigation.PushAsync(new MainPage(selected, chats)); // для перехода в будущем.
		//await DisplayAlert("Чат выбран", $"Выбран{selected.Name}", "OK");
	}
}