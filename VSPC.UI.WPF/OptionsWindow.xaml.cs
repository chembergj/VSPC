using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using VSPC.Common;

namespace VSPC.UI.WPF
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class OptionsWindow : Window
    {
        public OptionsWindow()
        {
            InitializeComponent();
        }

		private void buttonOk_Click(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default.Save();
			AIModelRuleRepository.SaveRules();
			Close();
		}

		private void buttonCancel_Click(object sender, RoutedEventArgs e)
		{
			if(MessageBox.Show("Do you really want to cancel - any changes will be discarded?", "VSPC Options", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
			{
				Properties.Settings.Default.Reload();
				Close();
			}
		}

		private void ButtonNewAIRule_Click(object sender, RoutedEventArgs e)
		{
			((ObservableCollection<AIModelRule>)dataGrid.ItemsSource).Insert(dataGrid.SelectedIndex, new AIModelRule());
		}

		private void Button_DeleteAIRuleClick(object sender, RoutedEventArgs e)
		{
			var itemsToDelete = dataGrid.SelectedItems.OfType<AIModelRule>().ToList();
			itemsToDelete.ForEach(i => AIModelRuleRepository.AllRules.Remove(i));
		}

		private void ButtonUp_Click(object sender, RoutedEventArgs e)
		{
			var idx = dataGrid.SelectedIndex;
			if (idx > 0 && idx < dataGrid.Items.Count)
			{
				AIModelRuleRepository.AllRules.Move(idx, idx - 1);
			}
		}

		private void ButtonDown_Click(object sender, RoutedEventArgs e)
		{
			var idx = dataGrid.SelectedIndex;
			if (idx > -1 && idx < AIModelRuleRepository.AllRules.Count - 1)
			{
				AIModelRuleRepository.AllRules.Move(idx, idx + 1);
			}
		}

		private void tabAIModel_Loaded(object sender, RoutedEventArgs e)
		{
			dataGrid.SelectedIndex = 0;
		}

		private void ButtonSearch_Click(object sender, RoutedEventArgs e)
		{
			if(string.IsNullOrEmpty(tbSearch.Text)) return;

			int currIndex = dataGrid.SelectedIndex == -1 ? 0 : dataGrid.SelectedIndex;

			var foundItem = AIModelRuleRepository.FindNext(currIndex + 1, tbSearch.Text);
			if(foundItem != null)
				dataGrid.SelectedItem = foundItem;
		}
    }
}
