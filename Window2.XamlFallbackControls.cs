using System.Windows.Controls;

namespace Label_CRM_demo;

public partial class Window2
{
    // Fallback fields for dashboard controls that are not currently named in XAML.
    private readonly Button OpenSelectedArtistButton = new Button();
    private readonly Button OpenArtistWorkspaceButton = new Button();
    private readonly TextBox DataWatchSearchBox = new TextBox();
    private readonly TextBlock DataWatchSelectedNameText = new TextBlock();
    private readonly TextBlock DataWatchSelectedMetaText = new TextBlock();
    private readonly TextBlock DataWatchSelectedContactText = new TextBlock();
    private readonly TextBlock DataWatchSelectedFollowUpText = new TextBlock();
    private readonly TextBlock DataWatchSelectedNotesText = new TextBlock();
    private readonly TextBlock ArtistTrackerStatusText = new TextBlock();
    private readonly TextBox AccountsSearchBox = new TextBox();
    private readonly ComboBox AccountsAccessFilterBox = new ComboBox();
    private readonly TextBox ContactsSearchBox = new TextBox();
    private readonly ComboBox ContactsFollowUpFilterBox = new ComboBox();
    private readonly TextBox ContractsSearchBox = new TextBox();
    private readonly ComboBox ContractsStatusFilterBox = new ComboBox();
    private readonly ItemsControl SupportConversationList = new ItemsControl();
    private readonly TextBlock SupportConversationCountText = new TextBlock();
    private readonly TextBlock SupportConversationMetaText = new TextBlock();
    private readonly ScrollViewer SupportConversationScrollViewer = new ScrollViewer();
}
