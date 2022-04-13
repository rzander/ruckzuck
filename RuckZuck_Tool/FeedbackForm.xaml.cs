using System.Windows;

namespace RuckZuck_Tool
{
    /// <summary>
    /// Interaction logic for FeedbackForm.xaml
    /// </summary>
    public partial class FeedbackForm : Window
    {
        public bool isWorking = false;
        public bool hasFeedback = false;

        public FeedbackForm()
        {
            InitializeComponent();
        }

        private void btYes_Click(object sender, RoutedEventArgs e)
        {
            tbFeedback.IsEnabled = false;
            tbFeedback.Text = "";
            btSend.IsEnabled = true;
            btYes.IsDefault = false;
            btSend.IsDefault = true;
            isWorking = true;
        }

        private void btNo_Click(object sender, RoutedEventArgs e)
        {
            tbFeedback.IsEnabled = true;
            btSend.IsEnabled = true;
            isWorking = false;
        }

        private void btSend_Click(object sender, RoutedEventArgs e)
        {
            hasFeedback = true;
            this.Close();
        }
    }
}
