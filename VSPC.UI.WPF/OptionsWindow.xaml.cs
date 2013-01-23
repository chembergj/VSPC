using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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
			AIModelRuleRepository.AllRules.Add(new AIModelRule());
		}

		private void Button_DeleteAIRuleClick(object sender, RoutedEventArgs e)
		{
			var itemsToDelete = new List<AIModelRule>(dataGrid.SelectedItems.Cast<AIModelRule>());
			itemsToDelete.ForEach(i => dataGrid.Items.Remove(i));
		}

		private void ButtonUp_Click(object sender, RoutedEventArgs e)
		{
			var idx = dataGrid.SelectedIndex;
			if (idx > 0)
			{
				var item = (AIModelRule)dataGrid.SelectedItem;

				AIModelRuleRepository.AllRules.RemoveAt(idx);
				AIModelRuleRepository.AllRules.Insert(idx - 1, item);
				dataGrid.SelectedIndex = idx - 1;
			}
		}

		private void ButtonDown_Click(object sender, RoutedEventArgs e)
		{
			var idx = dataGrid.SelectedIndex;
			if (idx > -1 && idx < dataGrid.Items.Count)
			{
				var item = (AIModelRule)dataGrid.SelectedItem;

				AIModelRuleRepository.AllRules.RemoveAt(idx);
				if (idx == AIModelRuleRepository.AllRules.Count)
					AIModelRuleRepository.AllRules.Add(item);
				else
					AIModelRuleRepository.AllRules.Insert(idx + 1, item);
				
				//dataGrid.Items.MoveCurrentToNext();
			}
		}
    }
}
