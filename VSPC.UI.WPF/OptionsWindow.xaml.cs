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
    }
}
