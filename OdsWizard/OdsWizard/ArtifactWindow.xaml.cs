using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Windows.UI.Xaml.Controls;

namespace OdsWizard
{
    /// <summary>
    /// Interaction logic for ArtifactWindow.xaml
    /// </summary>
    
    /* ArtifactWindow - класс окна приложения, для просмотра и правки текста сформированных скриптов.
    * Используемые переменные:
    *   table - таблица для которой создан скрипт;
    *   artifact - сформированный скрипт;
    *   systeam - система для которой создан скрипт.
    */
    public partial class ArtifactWindow : Window
    {
        private TractTable table;
        private TractLayerArtifact artifact;
        private TractSystem system;
        public ArtifactWindow(TractTable tbl, Int32 rowNumber)
        {
            InitializeComponent();
            // Отображение скрипта
            table = tbl;
            artifact = tbl.Artifacts[rowNumber];
            system = TractData.TractSystems.First(t => t.TractSystemName == tbl.Layer);

            var art = tbl.Artifacts[rowNumber].SqlText;
            var flowDoc = new FlowDocument();

            Paragraph para = new Paragraph();
            para.Inlines.Add(new Run(art));
            flowDoc.Blocks.Add(para);
            richTextBox.Document = flowDoc;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            // Подключение к БД
            using (SqlConnection conn = new SqlConnection(system.ConnStr))
            {
                var sqlText = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd).Text;
                if (artifact.Type == "PROCEDURE" || artifact.Type == "CREATE SCRIPT")
                {
                    sqlText = "exec ('" + sqlText + "')";
                }
                sqlText = "set NOEXEC ON" + Environment.NewLine + sqlText + Environment.NewLine + "set NOEXEC OFF";

                SqlCommand cmd = new SqlCommand(sqlText, conn);
                conn.Open();
            // Выполнение скрипта на сервере   
                try
                { 
                    cmd.ExecuteNonQuery(); 
                    artifact.SqlText = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd).Text;
                }
                catch (Exception ex)
                { 
                    MessageBox.Show(ex.Message); 
                }
            }
        }
    }
}
