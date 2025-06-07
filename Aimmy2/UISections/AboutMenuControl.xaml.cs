using System.Windows;
using System.Windows.Controls;
using Aimmy2.UILibrary;
using Other;

namespace Aimmy2.Controls
{
    public partial class AboutMenuControl : UserControl
    {
        private MainWindow? _mainWindow;
        private bool _isInitialized;

        // Credits data structure for cleaner organization
        private static readonly (string category, (string name, string role)[] members)[] CreditsData =
        {
            ("Developers", new[]
            {
                ("Babyhamsta", "AI Logic"),
                ("MarsQQ", "Design"),
                ("Taylor", "Optimization, Cleanup")
            }),
            ("Contributors", new[]
            {
                ("Shall0e", "Prediction Method"),
                ("wisethef0x", "EMA Prediction Method"),
                ("whoswhip", "Bug fixes & EMA"),
                ("HakaCat", "Idea for Auto Labelling Data"),
                ("Themida", "LGHub check"),
                ("Ninja", "MarsQQ's emotional support")
            }),
            ("Model Creators", new[]
            {
                ("Babyhamsta", "Universal, Phantom Forces"),
                ("Natdog400", "AIO"),
                ("Themida", "Arsenal, Strucid, Bad Business, Blade Ball, etc."),
                ("Hogthewog", "Da Hood, FN, etc.")
            })
        };

        // Public properties for MainWindow access
        public Label AboutSpecsControl => AboutSpecs;
        public ScrollViewer AboutMenuScrollViewer => AboutMenu;

        public AboutMenuControl()
        {
            InitializeComponent();
        }

        public void Initialize(MainWindow mainWindow)
        {
            if (_isInitialized) return;

            _mainWindow = mainWindow;
            _isInitialized = true;

            LoadCreditsMenu();
        }

        private void LoadCreditsMenu()
        {
            CreditsPanel.Children.Clear();

            for (int i = 0; i < CreditsData.Length; i++)
            {
                var (category, members) = CreditsData[i];

                // Add category title
                CreditsPanel.Children.Add(new ATitle(category, false));

                // Add members
                foreach (var (name, role) in members)
                {
                    CreditsPanel.Children.Add(new ACredit(name, role));
                }

                if (i < CreditsData.Length)
                {
                    CreditsPanel.Children.Add(new ARectangleBottom());
                    CreditsPanel.Children.Add(new ASpacer());
                }
            }
        }

        private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var updateManager = new UpdateManager();
                await updateManager.CheckForUpdate(AboutDesc.Content.ToString()); // Programically grab the version
                updateManager.Dispose();
            }
            catch (Exception ex)
            {
            }
        }
    }
}