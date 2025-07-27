using HomeGPT_Messenger.Models;
using HomeGPT_Messenger.Services;
using System;

namespace HomeGPT_Messenger.Pages;

public partial class ChatsPage : ContentPage
{
    private Chat chatForMenu;//��� ���������� ������ ���� ������ ��������/��������������.

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
		string name = await DisplayPromptAsync("����� ���", "������� �������� ����: ");
		if (string.IsNullOrWhiteSpace(name)) return;
		var chat=new Chat { Name= name };
		chats.Add(chat);
		await ChatStorageService.SaveChatsAsync(chats);
        ChatsList.ItemsSource = null;
        ChatsList.ItemsSource = chats;
    }

	private async void OnChatSelected(object sender, SelectionChangedEventArgs e)
	{
		var selected = e.CurrentSelection.FirstOrDefault() as Chat;
		if (selected == null) return;

		await Navigation.PushAsync(new MainPage(selected, chats)); // ������� + ��������
	}

	private async void OnChatMenuClicked(object sender, EventArgs e)
	{
		if (sender is Button btn && btn.CommandParameter is Chat chat)
		{
			chatForMenu = chat;//��� ������ ���� ����
			ChatMenuPopup.IsVisible = true;// �������� ����
		}
	}
    #region Overlay (��� ����)
    private void OnMenuOverlayTapped(object sender, EventArgs e)
    {
        SideMenu.IsVisible = false;
    }
    #endregion
    #region ButtonMenu (������ ����)
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
    #region PopupMenu (������ �������� � Rename)
    private async void Popup_RenamePressed(Object sender, EventArgs e)
	{
		ChatMenuPopup.IsVisible = false;
        string newName = await DisplayPromptAsync("������������� ���", "����� ��� ����:", initialValue: chatForMenu.Name);
		if (!string.IsNullOrWhiteSpace(newName))
		{
			chatForMenu.Name = newName;
			await ChatStorageService.SaveChatsAsync(chats);
			ChatsList.ItemsSource = null;//��� ����������� ����������
			ChatsList.ItemsSource = chats;
		}
	}
    private async void Popup_DeletePressed(Object sender, EventArgs e)
    {
        ChatMenuPopup.IsVisible = false;
        bool confirm = await DisplayAlert("������� ���", $"������� \"{chatForMenu.Name}\"?", "��", "���");
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
    #endregion
}