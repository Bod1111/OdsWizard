/* Дипломный проект
 * Тема: "Разработка системы для автоматизации подготовки загрузки первичных данных в хранилище".
 * 
 * Разработал: Никифоров Макар Александрович, группа ТИП-81.
 * Дата и номер версии: 15.05.2022 v1.3.
 * Язык программирования: С#.
 * Среда разработки: Visual Studio 2019.
 * *************************************************************************************
 * Задание:
 *  Разработать программную систему для автоматизирвоания процесса подготовки загрзуки 
 *  первичных данных в хранилище данных, путем автоматического формирования sql скриптов
 *  обхектов, необходимых при импорте данных. Реализовать понятный пользовательский интерфейс.
 * Основные переменные:
 *  currentPageChanged - флаг, отвечает за состояние текущей страницы;
 *  currentSystem - объект класса TractSystem. Хранит текущую систему, отображаемую в интерфейсе;
 *  pageChanged - флаг, отвечает за состояние страницы Wizard.
 * Ожидаемые входные данные:
 *  1) наименование системы источника;
 *  2) метаданные таблицы источника;
 *  3) путь сохранения файлов.
 */


using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Data.SqlClient;
using System.Configuration;
using System.Collections.Specialized;
using System.Data;
using Xceed.Wpf.Toolkit;
using System.Collections.ObjectModel;
//using Avalon.Windows.Dialogs;
using WinForms = System.Windows.Forms;
using System.IO;
using Oracle.DataAccess.Client;

namespace OdsWizard
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Boolean currentPageChanged = false;
        private Boolean canNext = false;
        private TractSystem currentSystem;
        private bool pageChanged;
        public MainWindow()
        {
            try
            {
                InitializeComponent();
                pageChanged = false;
                MainWizard.CanSelectNextPage = false;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.InnerException.Message);
            }
            TractData.TractSystems = new List<TractSystem>();
            string connString = ConfigurationManager.AppSettings.Get("configConnectionStr");
            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {

                                                                            // Получение всех доступных систем из БД 
                    string query = @"
select 
    id_layer as TR_SYSTEM_ID,
    name as TR_SYSTEM_NAME,
    name_s,
    GET_METADATA_SCRIPT,
    CONN_STRING,
    TR_SYSTEM_TYPE,
    isnull(IS_EXTERNAL, 0) IS_EXTERNAL,
    READONLY_DT_COLS,
    HIDDEN_DT_COLS,
    isnull(ADD_ROW_CFG, '0,0,0') ADD_ROW_CFG,
    isnull(DELETE_ROW_CFG, '0,0,0') DELETE_ROW_CFG,
    isnull(SHOW_INC, 0) SHOW_INC
from
    dbo.DIC_LAYER
order by IS_EXTERNAL desc, id_layer asc;";
                    SqlCommand cmd = new SqlCommand(query, conn);
                    conn.Open();
                    SqlDataReader dr = cmd.ExecuteReader();
                    if (dr.HasRows)
                    {
                        startPointCB.Items.Clear();
                        startPointCB.SelectedValuePath = "Key";
                        startPointCB.DisplayMemberPath = "Value";
                        while (dr.Read())                                      // Добавление полученных данных в переменную списка таблиц
                        {
                            TractSystem ts = new TractSystem()
                            {
                                TractSystemId = Convert.ToInt32(dr["TR_SYSTEM_ID"]),
                                TractSystemName = dr["TR_SYSTEM_NAME"].ToString(),
                                ScriptGetTableMetadata = dr["GET_METADATA_SCRIPT"].ToString(),
                                ConnStr = (Boolean)dr["IS_EXTERNAL"] ? dr["CONN_STRING"].ToString() : Tools.ReplaceServer(dr["CONN_STRING"].ToString()),
                                TractSystemType = dr["TR_SYSTEM_TYPE"].ToString(),
                                IsExternal = (Boolean)dr["IS_EXTERNAL"],
                                TractSystemDestinations = new List<TractSystem>(),
                                RoColumns = Array.ConvertAll(dr["READONLY_DT_COLS"].ToString().Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries), s => int.Parse(s)).ToList<int>(),
                                HiddenColumns = Array.ConvertAll(dr["HIDDEN_DT_COLS"].ToString().Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries), s => int.Parse(s)).ToList<int>(),
                                IsShowInc = (bool)dr["SHOW_INC"],
                                CanAddRowField = Convert.ToBoolean(Convert.ToInt32(dr["ADD_ROW_CFG"].ToString().Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)[0])),
                                CanAddRowComment = Convert.ToBoolean(Convert.ToInt32(dr["ADD_ROW_CFG"].ToString().Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)[1])),
                                CanAddRowArtifact = Convert.ToBoolean(Convert.ToInt32(dr["ADD_ROW_CFG"].ToString().Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)[2])),
                                CanDeleteRowField = Convert.ToBoolean(Convert.ToInt32(dr["DELETE_ROW_CFG"].ToString().Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)[0])),
                                CanDeleteRowComment = Convert.ToBoolean(Convert.ToInt32(dr["DELETE_ROW_CFG"].ToString().Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)[1])),
                                CanDeleteRowArtifact = Convert.ToBoolean(Convert.ToInt32(dr["DELETE_ROW_CFG"].ToString().Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)[2]))
                            };
                            TractData.TractSystems.Add(ts);
                            if (ts.TractSystemName != "OLAP+KIHFD")
                            {
                                startPointCB.Items.Add(new KeyValuePair<int, String>(ts.TractSystemId, ts.NameWithServer));
                            }
                        }
                        dr.Close();

                        foreach (TractSystem ts in TractData.TractSystems)           // Заполнение поля система назначения каждой из систем
                        {
                            query = "select id_layer_dst from [dbo].[DIC_TRANSITION_LAYER] where id_layer_src = " + ts.TractSystemId.ToString();
                            SqlCommand subCmd = new SqlCommand(query, conn);
                            SqlDataReader sdr = subCmd.ExecuteReader();
                            while (sdr.Read())
                            {
                                TractSystem dst = TractData.TractSystems.FirstOrDefault(t => t.TractSystemId == Convert.ToInt32(sdr["id_layer_dst"]));
                                if (dst != null)
                                    ts.TractSystemDestinations.Add(dst);
                            }
                            sdr.Close();
                        }
                    }
                    else
                    {
                        System.Windows.MessageBox.Show(@"В данный момент таблица с ситемами пуста!");
                    }

                    TractData.TypeTrans = new List<KeyValuePair<string, string>>();
                    cmd = new SqlCommand("select type_src, type_dst from DIC_TRANSITION_TYPE", conn);
                    SqlDataReader typeTransDr = cmd.ExecuteReader();
                    while (typeTransDr.Read())
                    {
                        TractData.TypeTrans.Add(new KeyValuePair<string, string>(typeTransDr["type_src"].ToString().ToUpper(), typeTransDr["type_dst"].ToString().ToUpper()));
                    }
                    typeTransDr.Close();
                }
            }
            catch (Exception Ex)
            {
                System.Windows.MessageBox.Show(@"Нет связи с сервером проверьте файл App.Config!");
                System.Windows.MessageBox.Show(Ex.Message);
            }
        }

        private void startPreviewBtn_Click(object sender, RoutedEventArgs e)
        {
            if (startPointCB.SelectedItem == null || String.IsNullOrEmpty(tableNameTB.Text))
            {
                System.Windows.MessageBox.Show("Необходимо выбрать систему и ввести наименование таблицы");
                return;
            }
            var rslt = MessageBoxResult.Cancel;
            if (TractData.TractTablesBuffer != null && TractData.TractTablesBuffer.Count() > 1 && currentPageChanged)
                rslt = System.Windows.MessageBox.Show("Уверены, что хотите очистить весь тракт?", "Внимание!!!", MessageBoxButton.OKCancel);

            if (TractData.TractTablesBuffer == null || TractData.TractTablesBuffer.Count() == 0 || rslt == MessageBoxResult.OK)
            {
                if (InitTract())
                {
                    TractTable currenTable = TractData.TractTablesBuffer.First(t => t.WzrdPage == IntroPage);
                    drawPage(IntroPage, currenTable);

                    MainWizard.CanSelectNextPage = true;
                }
            }
        }

        private void Wizard_Next(object sender, Xceed.Wpf.Toolkit.Core.CancelRoutedEventArgs e)
        {
            var currentPage = ((Wizard)sender).CurrentPage;
            TractTable tt = TractData.TractTablesBuffer.First(t => t.Layer == currentSystem.TractSystemName);

            //перезаполняем значения TractTable по странице
            String prefix = currentPage.Name.Replace("Page", "");
            ComboBox incCB = (ComboBox)currentPage.FindName(prefix + "IncCB");
            tt.IsIncrement = incCB.Text == "Increment";

            if (currentPage == OdsRawPage || currentPage == CalcfdPage)
            {
                ComboBox syncCB = (ComboBox)currentPage.FindName(prefix + "SyncCB");
                tt.SyncType = syncCB.Text;
            }

            //валидируем
            if (currentPage == IntroPage)
            {
                TractData.StartLayerName = currentSystem.TractSystemName;
            }
            else
            {
                //проверка корректности sql scripts
                if (currentPage != IntroPage)
                {
                    foreach (TractLayerArtifact art in tt.Artifacts)
                    {
                        using (SqlConnection conn = new SqlConnection(currentSystem.ConnStr))
                        {
                            var sqlArtText = art.SqlText.Replace("\nGO", "\n;");
                            if (art.Type == "PROCEDURE" || art.Type == "CREATE SCRIPT" || art.Type == "TRIGGER")
                            {
                                sqlArtText = "exec ('" + sqlArtText.Replace("'", "''") + "')";
                            }
                            sqlArtText = "set NOEXEC ON" + Environment.NewLine + sqlArtText + Environment.NewLine + "set NOEXEC OFF";

                            SqlCommand artCmd = new SqlCommand(sqlArtText, conn);
                            conn.Open();

                            try
                            {
                                artCmd.ExecuteNonQuery();
                            }
                            catch (Exception ex)
                            {
                                System.Windows.MessageBox.Show(ex.Message, "Проверьте артефакт " + art.Type);
                                e.Cancel = true;
                            }
                        }
                    }
                    //using (SqlConnection conn = new SqlConnection(currentSystem.ConnStr))
                    //{
                    //    var ttText = tt.SqlText;
                    //    ttText = "exec ('" + ttText + "')";
                    //    ttText = "set NOEXEC ON" + Environment.NewLine + ttText + Environment.NewLine + "set NOEXEC OFF";

                    //    SqlCommand ttCmd = new SqlCommand(ttText, conn);
                    //    conn.Open();

                    //    try
                    //    {
                    //        ttCmd.ExecuteNonQuery();
                    //    }
                    //    catch (Exception ex)
                    //    {
                    //        System.Windows.MessageBox.Show(ex.Message, "Скрипт создания таблицы неверен!");
                    //        e.Cancel = true;
                    //    }
                    //}
                }
            }
        }

        private bool InitTract()
        {
            bool initTract = false;
            //инициализируем буфер
            TractData.TractTablesBuffer = new List<TractTable>();

            String tableSchema = tableNameTB.Text.Contains(".") ? tableNameTB.Text.Split('.')[0].Replace("[", "").Replace("]", "") : "dbo";
            String tableName = tableNameTB.Text.Contains(".") ? tableNameTB.Text.Split('.')[1].Replace("[", "").Replace("]", "").Replace("\"", "") : tableNameTB.Text;
            TractTable tt = new TractTable()
            {
                TSchema = tableSchema,
                TName = tableName,
                TExtProps = new ObservableCollection<TractLayerExtProp>(),
                TColumns = new ObservableCollection<TractTableColumn>(),
                Artifacts = new ObservableCollection<TractLayerArtifact>(),
                IsExternal = currentSystem.IsExternal,
                WzrdPage = IntroPage,
                Layer = currentSystem.TractSystemName
            };

            initTract = true;
            // Инициализация тракта в зависимоти от типа системы
            if (currentSystem.TractSystemType != "Excel")
            {
                if (currentSystem.TractSystemType == "mssql")
                {
                    using (SqlConnection conn = new SqlConnection(currentSystem.ConnStr))
                    {
                        String query = currentSystem.ScriptGetTableMetadata.Replace("#schema#", tableSchema).Replace("#tname#", tableName);
                        SqlCommand cmd = new SqlCommand(query, conn);
                        conn.Open();
                        SqlDataReader dr = cmd.ExecuteReader();
                        if (dr.HasRows)
                        {
                            while (dr.Read())
                            {
                                if (String.IsNullOrEmpty(dr["tp"].ToString()))
                                {
                                    tt.TExtProps.Add(new TractLayerExtProp() { Name = dr["c_name"].ToString(), Value = dr["value"].ToString() });
                                }
                                else
                                {
                                    tt.TColumns.Add(new TractTableColumn()
                                    {
                                        CName = dr["c_name"].ToString(),
                                        CType = dr["tp"].ToString(),
                                        CTypeNew = dr["type_new"].ToString(),
                                        PrimaryKeyNumber = System.DBNull.Value == dr["pk"] ? null : (Int32?)dr["pk"],
                                        CComment = dr["value"].ToString()
                                    });
                                }
                            }
                        }
                        else
                        {
                            System.Windows.MessageBox.Show("Запрос не вернул данных");
                            initTract = false;
                        }
                    }
                }
                else if (currentSystem.TractSystemType == "oracle")
                {
                    String oraConnStr = currentSystem.ConnStr;
                    foreach (string key in ConfigurationManager.AppSettings)
                    {
                        oraConnStr = oraConnStr.Replace("#" + key + "#", ConfigurationManager.AppSettings[key]);
                    }
                    using (OracleConnection conn = new OracleConnection(oraConnStr))
                    {
                        String query = currentSystem.ScriptGetTableMetadata.Replace("#schema#", tableSchema.ToUpper()).Replace("#tname#", tableName.ToUpper());
                        OracleCommand cmd = new OracleCommand(query, conn);
                        conn.Open();

                        OracleDataReader ord = cmd.ExecuteReader();
                        if (ord.HasRows)
                        {
                            while (ord.Read())
                            {
                                if (String.IsNullOrEmpty(ord["tp"].ToString()))
                                {
                                    tt.TExtProps.Add(new TractLayerExtProp() { Name = ord["c_name"].ToString(), Value = ord["value"].ToString() });
                                }
                                else
                                {
                                    tt.TColumns.Add(new TractTableColumn()
                                    {
                                        CName = ord["c_name"].ToString(),
                                        CType = ord["tp"].ToString(),
                                        CTypeNew = ord["type_new"].ToString(),
                                        PrimaryKeyNumber = System.DBNull.Value == ord["pk"] ? null : (Int32?)(Decimal.ToInt32((decimal)ord["pk"])),
                                        CComment = ord["value"].ToString()
                                    });
                                }
                            }
                        }
                        else
                        {
                            System.Windows.MessageBox.Show("Запрос не вернул данных");
                            initTract = false;
                        }
                    }
                }
            }
            if (initTract) TractData.TractTablesBuffer.Add(tt);
            return initTract;
        }

        private void tableNameTB_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void Wizard_Previous(object sender, Xceed.Wpf.Toolkit.Core.CancelRoutedEventArgs e)
        {
            currentPageChanged = false;
        }

        private void IntroPage_Enter(object sender, RoutedEventArgs e)
        {
            currentPageChanged = true;

        }

        private void startPointCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            pageChanged = true;
            currentPageChanged = true;

            currentSystem = TractData.TractSystems.First(t => t.TractSystemId == ((KeyValuePair<int, String>)startPointCB.SelectedItem).Key);

            // Изменение системы приемника
            IntroPage_destinationComboBox.Items.Clear();
            IntroPage_destinationComboBox.SelectedValuePath = "Key";
            IntroPage_destinationComboBox.DisplayMemberPath = "Value";
            foreach (TractSystem ts in currentSystem.TractSystemDestinations)
            {
                IntroPage_destinationComboBox.Items.Add(new KeyValuePair<int, String>(ts.TractSystemId, ts.NameWithServer));
            }
            IntroPage_destinationComboBox.SelectedIndex = 0;
            // Выбор следующей страницы в зависимости от системы приемника
            switch (currentSystem.TractSystemName)
            {
                case "ODS.STG":
                    IntroPage.NextPage = OdsRawPage;
                    OdsRawPage.PreviousPage = IntroPage;
                    break;
                case "ODS.RAW":
                    IntroPage.NextPage = OdsExPage;
                    OdsExPage.PreviousPage = IntroPage;
                    break;
                case "ODS.EX":
                    IntroPage.NextPage = StagefdPage;
                    StagefdPage.PreviousPage = IntroPage;
                    break;
                case "STAGEFD":
                    IntroPage.NextPage = CalcfdPage;
                    CalcfdPage.PreviousPage = IntroPage;
                    break;
                case "CALCFD":
                    IntroPage.NextPage = OlapPage;
                    OlapPage.PreviousPage = IntroPage;
                    break;
                default:
                    IntroPage.NextPage = OdsStgPage;
                    break;
            };

            if (currentSystem.TractSystemType == "Excel")
                {
                IntroFieldsDG.IsReadOnly = false;
                IntroFieldsDG.CanUserAddRows = true;
                startPreviewBtn.Content = "Create";
                IntroCommentLB.CanUserAddRows = true;

                IntroPage.Description =
                    "Выбор стартовой точки и тестового сервера" + Environment.NewLine +
                    "Опишите сущность, как она есть в источнике";
            }
            else
            {
                IntroFieldsDG.IsReadOnly = true;
                IntroFieldsDG.CanUserAddRows = false;
                startPreviewBtn.Content = "Preview";
                IntroCommentLB.CanUserAddRows = true;

                IntroPage.Description =
                    "Выбор стартовой точки и тестового сервера" + Environment.NewLine +
                    "Описание сущности в источнике";
            }
            if (currentSystem.IsExternal)
                IntroIncCB.Visibility = Visibility.Collapsed;
            else
                IntroIncCB.Visibility = currentSystem.IsShowInc ? Visibility.Visible : Visibility.Collapsed;
            IntroNextLbl.Visibility = IntroIncCB.Visibility;
            anyChangeOnPage();
        }

        private void anyChangeOnPage()
        {
            PageProps pp = (PageProps)this.Resources["pageProperties"];
            if (this.MainWizard.CurrentPage == IntroPage)
            {
                if (IntroFieldsDG.Items.Count > 0 && IntroPage_destinationComboBox.SelectedItem != null)
                {
                    pp.CanNext = true;
                }
            }
        }

        private void drawPage(WizardPage wp, TractTable currenTable)
        {
            String prefix = wp.Name.Replace("Page", "");
            // Отрисовка интерфейса страницы
            DataGrid dg = (DataGrid)wp.FindName(prefix + "FieldsDG");
            dg.ItemsSource = currenTable.TColumns;
            dg.Columns[0].Header = "Primary Key Number";
            dg.Columns[1].Header = "Column Name";
            dg.Columns[2].Header = "Column Type";
            dg.Columns[3].Header = "New Column Name";
            dg.Columns[4].Header = "New Column Type";
            dg.Columns[5].Header = "Ext Properties Comment";

            dg.CellEditEnding += Dg_CellEditEnding;

            DataGrid commentLB = (DataGrid)wp.FindName(prefix + "CommentLB");
            commentLB.ItemsSource = currenTable.TExtProps;
            commentLB.CellEditEnding += Dg_CellEditEnding;

            DataGrid artifactLB = prefix == "Intro" ? new DataGrid() : (DataGrid)wp.FindName(prefix + "ArtifactLB");
            artifactLB.ItemsSource = currenTable.Artifacts;
            commentLB.CellEditEnding += Dg_CellEditEnding;
            if (prefix == "Intro")
            {
                // Отрисовка страницы IntroPage
                dg.Columns[3].Visibility = Visibility.Hidden;
                dg.Columns[4].Visibility = Visibility.Hidden;

                if (currentSystem.TractSystemType != "Excel")
                {
                    commentLB.CanUserAddRows = false;
                    commentLB.CanUserDeleteRows = false;

                    dg.CanUserAddRows = false;
                    dg.CanUserDeleteRows = false;

                    dg.IsReadOnly = false;
                    for (int i = 0; i < 6; i++)
                    {
                        dg.Columns[i].IsReadOnly = i == 5 || i == 0 ? false : true;
                    }
                    //dg.IsReadOnly = true;
                }
                else
                {
                    commentLB.CanUserAddRows = true;
                    commentLB.CanUserDeleteRows = true;
                    dg.IsReadOnly = false;

                    dg.CanUserAddRows = true;
                    dg.CanUserDeleteRows = true;
                    dg.IsReadOnly = false;
                }
            }
            else
            {
                // Отрисовка страниц слоев хранилища
                wp.Description = $"Объекты для создания в слое {currentSystem.NameWithServer}{Environment.NewLine}Основной объект {currenTable.TSchema}.{currenTable.TName}";
                foreach (int cIndex in currentSystem.RoColumns)
                {
                    dg.Columns[cIndex].IsReadOnly = true;
                }

                foreach (int cIndex in currentSystem.HiddenColumns)
                {
                    dg.Columns[cIndex].Visibility = Visibility.Hidden;
                }
                // не забыть убрать
                dg.Columns[3].Visibility = Visibility.Hidden;
                dg.Columns[4].Visibility = Visibility.Hidden;

                Style rowStyle = new Style(typeof(DataGridRow));
                rowStyle.Setters.Add(new EventSetter(DataGridRow.MouseDoubleClickEvent,
                                         new MouseButtonEventHandler(Row_DoubleClick)));
                artifactLB.RowStyle = rowStyle;

                if (currentSystem.TractSystemName != "OLAP+KIHFD")
                {
                    if (currentSystem.IsShowInc)
                        ((ComboBox)wp.FindName(prefix + "IncCB")).Visibility = Visibility.Visible;
                    else
                        ((ComboBox)wp.FindName(prefix + "IncCB")).Visibility = Visibility.Collapsed;
                }
            }
            if (currentSystem.TractSystemName != "OLAP+KIHFD")
            {
                // Отрисовка страницы на последнем слое хранлища
                Label lb = (Label)wp.FindName(prefix + "NextLbl");
                if (currentSystem.IsShowInc || currentSystem.TractSystemName == "ODS.RAW" || currentSystem.TractSystemName == "CALCFD")
                    lb.Visibility = Visibility.Visible;
                else
                    lb.Visibility = Visibility.Collapsed;
            }

            //!!!!!! обрублено после CALCFD
            //if (currentSystem.TractSystemName == "CALCFD")
            //{
            //    this.canNext = false;
            //    MainWizard.CanSelectNextPage = false;
            //}
        }

        private void Dg_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            pageChanged = true;
        }

        private void Row_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataGridRow row = sender as DataGridRow;
            TractTable currenTable = TractData.TractTablesBuffer.First(t => t.WzrdPage == MainWizard.CurrentPage);
            ArtifactWindow aw = new ArtifactWindow(currenTable, row.GetIndex());
            aw.ShowDialog();
        }

        private void generateTractTable(TractTable previousTable, WizardPage pg)
        {
            // Объявдение и инициализация переменных хранящих метаданные таблицы
            DataTable tableDT = new DataTable();
            tableDT.Columns.Add("id_tbl", typeof(int));
            tableDT.Columns.Add("schema_name_src", typeof(string));
            tableDT.Columns.Add("table_name_src", typeof(string));
            tableDT.Columns.Add("is_increment", typeof(int));
            tableDT.Columns.Add("schema_name_dst", typeof(string));
            tableDT.Columns.Add("table_name_dst", typeof(string));

            tableDT.Rows.Add(new object[] { 1, previousTable.TSchema, RenameExtSysName(previousTable.TName), previousTable.IsIncrement ? 1 : 0 });

            DataTable columnDT = new DataTable();
            columnDT.Columns.Add("id_table", typeof(int));
            columnDT.Columns.Add("clmn_src", typeof(string));
            columnDT.Columns.Add("ordr", typeof(int));
            columnDT.Columns.Add("typ_src", typeof(string));
            columnDT.Columns.Add("pk", typeof(int));
            columnDT.Columns.Add("clmn_dst", typeof(string));
            columnDT.Columns.Add("typ_dst", typeof(string));

            DataTable descrDT = new DataTable();
            descrDT.Columns.Add("id_table", typeof(int));
            descrDT.Columns.Add("id_column", typeof(int));
            descrDT.Columns.Add("ep_name", typeof(string));
            descrDT.Columns.Add("ep_value_src", typeof(string));
            descrDT.Columns.Add("ep_value_dst", typeof(string));

            int i = 1;
            foreach (var clmn in previousTable.TColumns)
            {
                columnDT.Rows.Add(new object[] { 1, RenameExtSysName(clmn.CName), i, RenameType(clmn.CType), clmn.PrimaryKeyNumber });
                descrDT.Rows.Add(new object[] { 1, i, "MS_Description", clmn.CComment });
                i += 1;
            }

            i = 1;
            foreach (var exprop in TractData.TractTablesBuffer[0].TExtProps)
            {
                descrDT.Rows.Add(new object[] { 1, 0, exprop.Name, exprop.Value });
                i += 1;
            }
            // Подключение к БД, вызов хранимой процедуры для формирования тракта для следующего слоя хранилища
            using (SqlConnection conn = new SqlConnection(ConfigurationManager.AppSettings.Get("configConnectionStr")))
            {
                SqlCommand cmd = new SqlCommand("dbo.create_layer_mdata @src_table_descr, @src_column_descr, @ep, @layer_name_src, @layer_name_dst, @ods_ex , @sync_type", conn);
                SqlParameter tblParam = cmd.Parameters.AddWithValue("@src_table_descr", tableDT);
                tblParam.SqlDbType = SqlDbType.Structured;
                tblParam.TypeName = "dbo.list_table";
                SqlParameter columnParam = cmd.Parameters.AddWithValue("@src_column_descr", columnDT);
                columnParam.SqlDbType = SqlDbType.Structured;
                columnParam.TypeName = "dbo.list_column";
                SqlParameter propsParam = cmd.Parameters.AddWithValue("@ep", descrDT);
                propsParam.SqlDbType = SqlDbType.Structured;
                propsParam.TypeName = "dbo.ext_props";
                SqlParameter srcParam = cmd.Parameters.AddWithValue("@layer_name_src", previousTable.Layer);
                srcParam.SqlDbType = SqlDbType.VarChar;
                SqlParameter dstParam = new SqlParameter();
                dstParam = cmd.Parameters.AddWithValue("@layer_name_dst", currentSystem.TractSystemName);
                dstParam.SqlDbType = SqlDbType.VarChar;
                if (previousTable.SyncType != null)
                {
                    SqlParameter odsExParam = cmd.Parameters.AddWithValue("@ods_ex", previousTable.SyncType);
                    dstParam.SqlDbType = SqlDbType.VarChar;
                    SqlParameter syncTypeParam = cmd.Parameters.AddWithValue("@sync_type", previousTable.SyncType);
                    dstParam.SqlDbType = SqlDbType.VarChar;
                }
                else
                {
                    SqlParameter odsExParam = cmd.Parameters.AddWithValue("@ods_ex", "");
                    dstParam.SqlDbType = SqlDbType.VarChar;
                    SqlParameter syncTypeParam = cmd.Parameters.AddWithValue("@sync_type","");
                    dstParam.SqlDbType = SqlDbType.VarChar;
                }

                conn.Open();
                SqlDataReader dr = cmd.ExecuteReader();
                // Парсинг полученных данных
                if (dr.HasRows)
                {
                    TractTable tt = new TractTable()
                    {
                        TExtProps = new ObservableCollection<TractLayerExtProp>(),
                        TColumns = new ObservableCollection<TractTableColumn>(),
                        Artifacts = new ObservableCollection<TractLayerArtifact>(),
                        IsExternal = false,
                        WzrdPage = pg,
                        Layer = currentSystem.TractSystemName
                    };

                    var isCreate = true;
                    while (dr.Read())
                    {
                        if (isCreate)
                        {
                            tt.TSchema = dr["SCHEMA_DST"].ToString();
                            tt.TName = dr["TABLE_DST"].ToString();
                            isCreate = false;
                        }
                        if (String.IsNullOrEmpty(dr["TYPE"].ToString()))
                        {
                            tt.TExtProps.Add(new TractLayerExtProp() { Name = dr["COLUMN_NAME"].ToString(), Value = dr["VALUE"].ToString() });
                        }
                        else if (String.IsNullOrEmpty(dr["COLUMN_ID"].ToString()))
                        {
                            tt.Artifacts.Add(new TractLayerArtifact()
                            {
                                Name = dr["COLUMN_NAME"].ToString(),
                                Type = dr["TYPE"].ToString(),
                                SqlText = dr["VALUE"].ToString(),
                                IsHandChanged = false
                            });
                        }
                        else
                        {
                            tt.TColumns.Add(new TractTableColumn()
                            {
                                CName = dr["COLUMN_NAME"].ToString(),
                                CType = dr["TYPE"].ToString(),
                                PrimaryKeyNumber = System.DBNull.Value == dr["PK"] ? null : (Int32?)dr["PK"],
                                CComment = dr["VALUE"].ToString()
                            });
                        }
                    }
                    if (TractData.TractTablesBuffer.Count(t => t.Layer == currentSystem.TractSystemName) > 0)
                    {
                        TractData.TractTablesBuffer[TractData.TractTablesBuffer.FindIndex(ind => ind.Layer == currentSystem.TractSystemName)] = tt;
                    }
                    else
                        TractData.TractTablesBuffer.Add(tt);
                }
            }
        }

        private bool needRunGenerateScript(WizardPage page)
        {
            return (MainWizard.CurrentPage == page && TractData.TractTablesBuffer.Where(t => t.WzrdPage == page).Count() == 0) || pageChanged;
        }

        private void OdsStgPage_Enter(object sender, RoutedEventArgs e)
        {
            // Присвоение перемееной curentSysteam значение следующей систмы и генерация тракта загрузки для нее
            WizardPage pg = (WizardPage)sender;
            TractTable previousTable = TractData.TractTablesBuffer.First(t => t.WzrdPage == pg.PreviousPage);
            currentSystem = TractData.TractSystems.First(t => t.TractSystemName == "ODS.STG");

            if (needRunGenerateScript(pg))
            {
                generateTractTable(previousTable, pg);
            }

            TractTable currenTable = TractData.TractTablesBuffer.First(t => t.WzrdPage == pg);
            drawPage(OdsStgPage, currenTable);
        }

        private void OdsRawPage_Enter(object sender, RoutedEventArgs e)
        {
            // Присвоение перемееной curentSysteam значение следующей систмы и генерация тракта загрузки для нее
            WizardPage pg = (WizardPage)sender;
            TractTable previousTable = TractData.TractTablesBuffer.First(t => t.WzrdPage == pg.PreviousPage);
            currentSystem = TractData.TractSystems.First(t => t.TractSystemName == "ODS.RAW");

            if (needRunGenerateScript(pg))
            {
                generateTractTable(previousTable, pg);
            }

            TractTable currenTable = TractData.TractTablesBuffer.First(t => t.WzrdPage == pg);
            drawPage(OdsRawPage, currenTable);
        }

        private void OdsExPage_Enter(object sender, RoutedEventArgs e)
        {
            // Присвоение перемееной curentSysteam значение следующей систмы и генерация тракта загрузки для нее
            WizardPage pg = (WizardPage)sender;
            TractTable previousTable = TractData.TractTablesBuffer.First(t => t.WzrdPage == pg.PreviousPage);
            currentSystem = TractData.TractSystems.First(t => t.TractSystemName == "ODS.EX");

            if (needRunGenerateScript(pg))
            {
                generateTractTable(previousTable, pg);
            }

            TractTable currenTable = TractData.TractTablesBuffer.First(t => t.WzrdPage == pg);
            drawPage(OdsExPage, currenTable);
        }

        private void StagefdPage_Enter(object sender, RoutedEventArgs e)
        {
            // Присвоение перемееной curentSysteam значение следующей систмы и генерация тракта загрузки для нее
            WizardPage pg = (WizardPage)sender;
            TractTable previousTable = TractData.TractTablesBuffer.First(t => t.WzrdPage == pg.PreviousPage);
            currentSystem = TractData.TractSystems.First(t => t.TractSystemName == "STAGEFD");

            if (needRunGenerateScript(pg))
            {
                generateTractTable(previousTable, pg);
            }

            TractTable currenTable = TractData.TractTablesBuffer.First(t => t.WzrdPage == pg);
            drawPage(StagefdPage, currenTable);
        }

        private void CalcfdPage_Enter(object sender, RoutedEventArgs e)
        {
            // Присвоение перемееной curentSysteam значение следующей систмы и генерация тракта загрузки для нее
            WizardPage pg = (WizardPage)sender;
            TractTable previousTable = TractData.TractTablesBuffer.First(t => t.WzrdPage == pg.PreviousPage);
            currentSystem = TractData.TractSystems.First(t => t.TractSystemName == "CALCFD");

            if (needRunGenerateScript(pg))
            {
                generateTractTable(previousTable, pg);
            }

            TractTable currenTable = TractData.TractTablesBuffer.First(t => t.WzrdPage == pg);
            drawPage(CalcfdPage, currenTable);
        }

        private void OlapPage_Enter(object sender, RoutedEventArgs e)
        {
            // Присвоение перемееной curentSysteam значение следующей систмы и генерация тракта загрузки для нее
            WizardPage pg = (WizardPage)sender;
			TractTable previousTable = TractData.TractTablesBuffer.First(t => t.WzrdPage == pg.PreviousPage);
			currentSystem = TractData.TractSystems.First(t => t.TractSystemName == "OLAP+KIHFD");

			if (needRunGenerateScript(pg))
			{
				generateTractTable(previousTable, pg);
			}

			TractTable currenTable = TractData.TractTablesBuffer.First(t => t.WzrdPage == pg);
			drawPage(OlapPage, currenTable);
			//Xceed.Wpf.Toolkit.MessageBox.Show("Здесь функционал не реализован");
        }

        private void MainWizard_Help(object sender, RoutedEventArgs e)
        {
            SaveScripts();
        }

        private void SaveScripts()
        {
            // Инициализация окна выбора директории для сохранения
            WinForms.FolderBrowserDialog fd = new WinForms.FolderBrowserDialog();
            WinForms.DialogResult result = fd.ShowDialog();
            if (result == WinForms.DialogResult.OK)
            {
                // Сохранения скриптов из буфера
                foreach (TractTable table in TractData.TractTablesBuffer)
                {
                    if (table.Layer != TractData.StartLayerName)
                    {
                        // Создание папки с названием системы
                        var layerDir = Directory.CreateDirectory(System.IO.Path.Combine(fd.SelectedPath, table.Layer));
                        TractTable previousTable = TractData.TractTablesBuffer.TakeWhile(x => x != table).DefaultIfEmpty(TractData.TractTablesBuffer[TractData.TractTablesBuffer.Count - 1]).LastOrDefault();
                        // Запись скриптов в файл
                        if (previousTable.SyncType != "VIEW")
                        {
                            using (StreamWriter sw = File.CreateText(System.IO.Path.Combine(fd.SelectedPath, layerDir.Name, String.Format("{0}.{1}.sql", table.TSchema, table.TName))))
                            {
                                sw.WriteLine(table.SqlText);
                            }
                        }
                        foreach (TractLayerArtifact art in table.Artifacts)
                        {
                            if (!new String[] { "CONSTRAINT", "INDEX", "TRIGGER" }.Contains(art.Type))
                            {
                                using (StreamWriter sw = File.CreateText(System.IO.Path.Combine(fd.SelectedPath, layerDir.Name, String.Format("{0}.sql", art.Name))))
                                {
                                    sw.WriteLine(art.SqlText);
                                }
                            }
                        }
                    }
                }
                this.Close();
            }
        }

        private void IntroCommentLB_AddingNewItem(object sender, AddingNewItemEventArgs e)
        {
            DataGrid dg = (DataGrid)sender;
            if (dg.Items.Count == 1)
            {
                dg.CanUserAddRows = false;
                dg.CanUserDeleteRows = false;
            }
        }

        private String RenameExtSysName(String seqName)
        {
            // Приведение к общему виду наименования системы
            String res = seqName;
            if (res.Contains("\"") || res.Contains("/") || res.Contains("."))
            {
                res = res.Replace("\"", "");
                if (res.StartsWith("/"))
                {
                    res = res.Remove(0, 1);
                }
                res = res.Replace("/", "_");
                res = res.Replace(".", "_");
            }
            return res;
        }

        private String RenameType(String oldType)
        {
            // Приведение к общему виду типов столбцов
            String res = oldType.ToUpper();
            foreach (KeyValuePair<String, String> pair in TractData.TypeTrans)
            {
                if (pair.Key == res)
                {
                    res = res.Replace(pair.Key, pair.Value);
                }
                else if (res.StartsWith(pair.Key + "("))
                {
                    res = res.Replace(pair.Key, pair.Value);
                }
            }
            return res;
        }

        private void MainWizard_Finish(object sender, Xceed.Wpf.Toolkit.Core.CancelRoutedEventArgs e)
        {
            // Обработка события нажатия на кнопку cansel
            SaveScripts();
        }
    }
}
