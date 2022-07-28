using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Xceed.Wpf.Toolkit;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace OdsWizard
{
    /* TractData - класс представляющий данные тракта      ++++++++++++++
     * Используемые переменные:
     *  TractSystems - список систем;
     *  TractTablesBuffer - буфер, хранящий таблицы;
     *  StartLayerName - наименование начальной системы;
     *  TypeTrans - тип транзации.
     */
    public static class TractData
    {
        public static List<TractSystem> TractSystems;
        public static List<TractTable> TractTablesBuffer;
        public static String StartLayerName;
        public static List<KeyValuePair<String, String>> TypeTrans;
    }

    /* TractSystem - класс представляющий данные о системе      ++++++++++
    * Используемые переменные:
    *  TractSystemId - id системы;
    *  TractSystemName - наименование системы;
    *  ScriptGetTableMetadata - скрипт получения метаданных таблицы;
    *  ConnStr - строка подключения;
    *  TractSystemType - тип ситемы;
    *  IsExternal - признак системы, внешняя/не внешняя;
    *  TractSystemDestinations - система назначения;
    *  RoColumns - список столбцов таблциы;
    *  HiddenColumns - список скрытых столбцов;
    *  CanAddRowComment - признак строки, можно ли добавить коментарий;
    *  CanAddRowField - признак строки, можно ли добавить поле;
    *  CanAddRowArtifact - признак строки, можно ли добавить артефакт;
    *  CanDeleteRowComment - признак строки, можно ли удалить коментарий;
    *  CanDeleteRowField - признак строки, можно ли удалить поле;
    *  CanDeleteRowArtifact - признак строки, можно ли удалить поле;
    *  IsShowInc - признак строки, отображать или нет;
    *  NameWithServer -наименование сервера ;
    */
    public class TractSystem
    {
        public Int32 TractSystemId;
        public String TractSystemName;
        public String ScriptGetTableMetadata;
        public String ConnStr;
        public String TractSystemType; //oracle/mssql
        public Boolean IsExternal;
        public List<TractSystem> TractSystemDestinations;
        public List<int> RoColumns;
        public List<int> HiddenColumns;
        public bool CanAddRowComment;
        public bool CanAddRowField;
        public bool CanAddRowArtifact;
        public bool CanDeleteRowComment;
        public bool CanDeleteRowField;
        public bool CanDeleteRowArtifact;
        public bool IsShowInc;
        public String NameWithServer
        {
            get
            {
                if (this.TractSystemType.ToLower() != "mssql")
                    return $"{this.TractSystemName} ({this.TractSystemType})";
                else
                {
                    SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(this.ConnStr);
                    return $"{this.TractSystemName} ({builder.DataSource})";
                }
            }
        }    
    }

    /* TractTableColumna - класс представляющий данные колонок таблиц. +++++++++++++++++
    * Используемые переменные:
    *  PrimaryKeyNumber - номер первиного ключа;
    *  CName - наименование слобца;
    *  CType - тип столбца;
    *  CComment - коментарии.
    */
    public class TractTableColumn
    {
        public Int32? PrimaryKeyNumber { get; set; }
        public String CName { get; set; }
        public String CType { get; set; }
        public String CNameNew { get; set; }
        public String CTypeNew { get; set; }
        public String CComment { get; set; }
    }

    /* TractLayerArtifact - класс представляющий артефакт слоя. ++++++++++++++
    * Используемые переменные:
    *  Name - наименование;
    *  Type - тип артефакта;
    *  SqlText - текст sql скрипта;
    */
    public class TractLayerArtifact
    {
        public String Name { get; set; }
        public String Type { get; set; }
        public String SqlText { get; set; }
        public Boolean IsHandChanged { get; set; }
    }

    /* TractLayerExtProp - класс представляющий артефакт слоя.   +++++++++++++++
    * Используемые переменные:
    *  _name - приватная перемнная, наименование;
    *  Name - открытая переменная, наименование;
    *  Value - текс описания;
    */
    public class TractLayerExtProp
    {
        private String _name;
        public String Name
        {
            get
            {
                if (String.IsNullOrEmpty(_name))
                {
                    _name = "MS_Description";
                }
                return _name;
            }
            set { _name = value; }
        }
        public String Value { get; set; }
    }

    /* TractTable - класс представляющий данные таблицы.           +++++++++++++++++++++++++++
    * Используемые переменные:
    *  TSchema - наименование схемы таблицы;
    *  TName - наименование таблицы;
    *  TExtProps - описание таблицы;
    *  TColumns - колонки таблицы;
    *  IsExternal - признак внешней системы;
    *  WzrdPage - страница соответсвующая данной таблице;
    *  Artifacts - артефакты таблицы;
    *  Layer - система;
    *  TSchemaNew - новое наименование схемы таблицы;
    *  TNameNew - новое наименование таблицы;
    *  SyncType - тип синхронизации;
    *  SqlText - sql текст скрипта для создания таблицы.
    */
    public class TractTable
    {
        public String TSchema;
        public String TName;
        public ObservableCollection<TractLayerExtProp> TExtProps;
        public ObservableCollection<TractTableColumn> TColumns;
        public Boolean IsExternal;
        public WizardPage WzrdPage;
        public ObservableCollection<TractLayerArtifact> Artifacts;
        public String Layer;

        public String TSchemaNew;
        public String TNameNew;

        public Boolean IsIncrement;

        public String SyncType;
        public String SqlText
        {
            get
            {
                string stext = "";
                string fieldComments = "";
                stext = "CREATE TABLE " + TSchema + "." + TName + "(" + Environment.NewLine;
                foreach (TractTableColumn clmn in this.TColumns)
                {
                    stext = stext + String.Format("	{0} {1}{2} NULL,", clmn.CName, clmn.CType, clmn.PrimaryKeyNumber == null ? "" : " NOT") + Environment.NewLine;
                    if (!String.IsNullOrEmpty(clmn.CComment))
                        fieldComments = fieldComments +
                            String.Format("EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'{0}' , @level0type=N'SCHEMA',@level0name=N'{1}', @level1type=N'TABLE',@level1name=N'{2}', @level2type=N'COLUMN',@level2name=N'{3}'", clmn.CComment, this.TSchema, this.TName, clmn.CName)
                            + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine;
                }
                stext = stext + Environment.NewLine + ")" + Environment.NewLine + "GO" + Environment.NewLine;
                stext = stext + fieldComments;

                bool msDescrAdded = false;
                foreach (TractLayerExtProp prp in this.TExtProps)
                {
                    if (!String.IsNullOrEmpty(prp.Name) && !String.IsNullOrEmpty(prp.Value) && ((prp.Name == "MS_Description" && !msDescrAdded) || prp.Name != "MS_Description"))
                    {
                        stext = stext +
                            String.Format(@"EXEC sys.sp_addextendedproperty @name=N'{0}', @value=N'{1}' , @level0type=N'SCHEMA',@level0name=N'{2}', @level1type=N'TABLE',@level1name=N'{3}'", prp.Name, prp.Value, this.TSchema, this.TName);
                        stext = stext + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine;
                        msDescrAdded = prp.Name == "MS_Description";
                    }
                }
                foreach (TractLayerArtifact art in this.Artifacts)
                {
                    if (new String[] { "CONSTRAINT", "INDEX", "TRIGGER" }.Contains(art.Type))
                    { 
                        stext = stext + art.SqlText + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine;
                    }
                }

                return stext;
            }
        }
    }

    public class PageProps : INotifyPropertyChanged
    {
        private bool _canNext;
        public bool CanNext
        {
            get { return _canNext; }
            set
            {
                _canNext = value;
                OnPropertyChanged("CanNext");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
    }

    // Tools - класс содержащий вспомогательные методы
    public static class Tools
    {
        public static String GetCleanScript(String ScriptText)
        {
            // Отчистка скрипта от лишних символов
            String res = ScriptText;
            res = res.
                Replace("[", "").
                Replace("]", "").
                Replace(Environment.NewLine, "").
                Replace("\t", "").
                Replace(" ", "");
            return res;
        }

        public static String ReplaceServer(String ConnStr)
        {
            // Вставка в строку подключения значений из файла App.config
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(ConnStr);
            if (!String.IsNullOrWhiteSpace(ConfigurationManager.AppSettings.Get(builder.DataSource)))
            {
                builder.DataSource = ConfigurationManager.AppSettings.Get(builder.DataSource);
            }
            return builder.ToString();
        }

        public static String CamelToConstant(String str)
        {

            const string pattern = @"(?<=\w)(?=[A-Z])";
            string result = Regex.Replace(str, pattern,
                "_", RegexOptions.None);
            return result.ToUpper();
        }
    }
}
