namespace HomeGPT_Messenger.Pages.Controls;

public partial class PopupMenu : ContentView
{
	public event EventHandler RenamePressed;
	public event EventHandler DeletePressed;
	public event EventHandler CancelPressed;
	public PopupMenu()
	{
		InitializeComponent();
	}
	private void RenameClicked(object sender, EventArgs e)=>RenamePressed?.Invoke(this, EventArgs.Empty);
    private void DeleteClicked(object sender, EventArgs e) => DeletePressed?.Invoke(this, EventArgs.Empty);
    private void CancelClicked(object sender, EventArgs e) => CancelPressed?.Invoke(this, EventArgs.Empty);
}