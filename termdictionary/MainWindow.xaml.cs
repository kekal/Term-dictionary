using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace termdictionary
{

    public partial class MainWindow
    {

        private static MainWindow _wm;

        public TaskManager _taskManager;

        public List<Ent> Entries = new List<Ent>();

        public string DictPath = "Translate БУ OL640.xml";

        public string SortDirection;

        public MainWindow()
        {
            InitializeComponent();

            LoadShowDict(DictPath);

            _wm = this;

            _taskManager = new TaskManager();
        }


        /// <summary> обработчик метода вызванного из списка заданий </summary>
        public static void InMainDispatch(Action dlg)
        {
            if (Thread.CurrentThread.Name == "Main Thread")
            {
                dlg();
            }
            else
            {
                _wm.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action<string>(delegate { dlg(); }), "?");
            }
        }

        /// <summary> генерирует xml файл из текстового с разметкой по табуляции </summary>
        public void TxtFileToXmlFileConverter(string path)
        {
            if (!PathCheck(path))
            {
                MessageBox.Show("Словарь не найден");
                return;
            }

            XmlDocument doc;

            TxtFileToXmlDoc(path, out doc);

            if (doc == null) return;

            DictPath = Path.GetFileNameWithoutExtension(path) + ".xml";

           
            try
            {
                doc.Save(DictPath); //сохраняем файл

                LoadShowDict(DictPath);
            }
            catch
            {
                MessageBox.Show("Файл словаря занят.\nПотвторить?", "Ошибка записи", MessageBoxButton.YesNo,
                    MessageBoxImage.Error, MessageBoxResult.No, MessageBoxOptions.DefaultDesktopOnly);


                //MessageBox.Show("Файл словаря занят.");
            }


        }

        /// <summary> создаёт XmlDocument из текстового файла разделённого табуляциями </summary>
        public void TxtFileToXmlDoc(string path, out XmlDocument doc)
        {
            if (!PathCheck(path))
            {
                MessageBox.Show("Словарь не найден");
                doc = new XmlDocument();
                return;
            }

            string plainText;

            try
            {
                plainText = File.ReadAllText(path);
            }
            catch (IOException e)
            {
                MessageBox.Show(e.Message);
                doc = null;
                return;
            }

            var textLines = plainText.Split('\n');

            doc = new XmlDocument();
            var xmlDecl = doc.CreateXmlDeclaration("1.0", "utf-8", null); //прописываем деклaрацию к файлу
            doc.AppendChild(xmlDecl); //добавляем в документ

            var rootElement = doc.CreateElement("ArrayOfEnt"); //Создаем родительский элемент записей

            foreach (var textLine in textLines)
            {
                var filds = textLine.Split('\t'); //разделяем строки на подстроки

                var ent = doc.CreateElement("Ent"); //Создаем подэллемент словарная статья

                var rus = doc.CreateElement("Rus");
                rus.InnerText = filds[0].Trim(); //записываем русское выражение во 1 поле

                var eng = doc.CreateElement("Eng");
                eng.InnerText = filds[1].Trim(); //записываем английское выражение во 2 поле


                ent.AppendChild(rus); //добавляем английское выражение
                ent.AppendChild(eng); //добавляем русское выражение

                rootElement.AppendChild(ent); //добавляем запись
            }

            doc.AppendChild(rootElement); //добавляем все записи в файл
        }

        public void LoadShowDict(string path)
        {
            if (!PathCheck(path))
            {
                MessageBox.Show("Словарь не найден");
                return;
            }

            XmlFileToList(path, out Entries);

            var listEntries = new List<Ent>(Entries);

            DatagridRefresh(listEntries);
        }

        public bool PathCheck(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }
            return true;
        }

        /// <summary> записывает xml файл в List of Ent </summary>
        public void XmlFileToList(string path, out List<Ent> list)
        {
            list = new List<Ent>();

            if (!PathCheck(path))
            {
                MessageBox.Show("Словарь не найден");

                return;
            }

            var stream = new FileStream(path, FileMode.Open, FileAccess.Read);

            var serializer = new XmlSerializer(typeof (List<Ent>));


            try
            {
                list = (List<Ent>) serializer.Deserialize(stream);
            }
            catch (Exception e)
            {
                MessageBox.Show("XML файл поврежёдён или недоступен.\n" + e.Message);
            }

            stream.Close();

            RefineList(ref list);

            SortEntList(ref list);
        }

        /// <summary> очистка недозаполненных статей. </summary>
        private void RefineList(ref List<Ent> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var ent = list[i];

                if (string.IsNullOrEmpty(ent.Eng) && string.IsNullOrEmpty(ent.Rus))
                {
                    list.Remove(ent);
                    i--;
                }
            }
        }
        
        /// <summary> сортировка по указанному столбцу (починить глобальные переменные) </summary>
        public void SortEntList(ref List<Ent> list)
        {
            list = list.OrderBy(o => SortDirection == "Русский" ? o.Rus : o.Eng).ToList();
        }
        
        /// <summary> (пере)запись указанного списка в таблицу </summary>
        public void DatagridRefresh(List<Ent> list)
        {
            if (VocabData == null) return;

            VocabData.ItemsSource = list;
        }
        
        /// <summary> диалог открытия файла указанного типа </summary>
        public string GetFilePathDialog(string format)
        {
            var ofd = new OpenFileDialog
            {
                Filter = format,
                FilterIndex = 1,
                Multiselect = false
            };

            var userClickedOk = ofd.ShowDialog();

            if (!(bool) userClickedOk) return "";

            return ofd.FileName;
        }
        
        /// <summary> запись экспорт текущего словаря в xml файл </summary>
        public void SaveXml(List<Ent> list, string path)
        {
            if (list == null || !PathCheck(path)) return;


            for (var i = 0; i < list.Count; i++)
            {
                var ent = list[i];
                if (ent.Rus == null && ent.Eng == null)
                {
                    list.Remove(ent);
                    i--;
                }

                if (ent.Rus != null) ent.Rus = ent.Rus.Trim().ToLower();
                if (ent.Eng != null) ent.Eng = ent.Eng.Trim().ToLower();
            }


            //list = (List<Ent>)VocabData.ItemsSource;
            
            //var stream = new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite);

            XmlWriter writer = new XmlTextWriter(path, Encoding.Unicode);
            var serializer = new XmlSerializer(typeof (List<Ent>));

            try
            {
                serializer.Serialize(writer, list);
            }
            catch (Exception e)
            {
                MessageBox.Show("Файл словаря отсуствует или недоступен.\n" + e.Message);
            }

            //stream.Close();
            writer.Close();
        }

        /// <summary> форматирует DataGrid </summary>
        public void FormatDatagrid()
        {
            if (VocabData.Columns.Count == 0) return;

            VocabData.Columns[0].Header = "English";
            VocabData.Columns[1].Header = "Русский";


            var columnwidth = VocabData.ActualWidth/2;

            VocabData.Columns[0].Width = columnwidth;
            VocabData.Columns[1].Width = columnwidth - 20;
        }




        /// <summary> кнопка вызова диалога импорта из xml файла </summary>
        private void ButtonOpenDictionary_Click(object sender, RoutedEventArgs e)
        {
            var path = GetFilePathDialog("Dictionary Files (.xml)|*.xml|All Files (*.*)|*.*");


            if (!PathCheck(path))
            {
                MessageBox.Show("Файл не выбран");
                return;
            }


            XmlFileToList(path, out Entries);

            DictPath = path;

            var listEntries = new List<Ent>(Entries);

            DatagridRefresh(listEntries);
        }

        /// <summary> кнопка вызова диалога импорта из txt файла </summary>
        private void ButtonOpenTXTFile_Click(object sender, RoutedEventArgs e)
        {

            var path = GetFilePathDialog("Text Files (.txt)|*.txt|All Files (*.*)|*.*");

            if (!PathCheck(path))
            {
                MessageBox.Show("Файл не выбран");
                return;
            }

            TxtFileToXmlFileConverter(path);

            path = Path.ChangeExtension(path, ".xml");

            XmlFileToList(path, out Entries);
        }

        /// <summary> переформатирует Datagrid при изменении размеров </summary>
        private void VocabData_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            FormatDatagrid();
        }

        /// <summary> редактирование ячейки </summary>
        private void VocabData_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (!string.IsNullOrEmpty(SearchText.Text)) return;

            var rowIndex = ((DataGrid) sender).ItemContainerGenerator.IndexFromContainer(e.Row);
            var colIndex = e.Column.DisplayIndex;

            var cellText = ((TextBox) (e.EditingElement)).Text;

            switch (colIndex)
            {
                case 0:
                    if (rowIndex >= Entries.Count) Entries.Add(new Ent {Eng = cellText, Rus = ""});

                    else Entries[rowIndex].Eng = cellText;

                    break;

                case 1:
                    if (rowIndex >= Entries.Count) Entries.Add(new Ent {Rus = cellText, Eng = ""});

                    else Entries[rowIndex].Rus = cellText;

                    break;
                default:
                    MessageBox.Show("Неясная колонка");
                    break;
            }


            SaveXml(Entries, DictPath);

            LoadShowDict(DictPath);

        }

        /// <summary> обработка ввода в поле поиска с формированием задачи сортировки в диспетчере </summary>
        private void SearchText_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = ((TextBox) sender).Text;

            if (query == null) return;

            VocabData.IsReadOnly = query != "";

            var currentEntries = Entries.Where(entry => 
                entry.Rus.ToLower().Contains(query.ToLower()) || 
                entry.Eng.ToLower().Contains(query.ToLower())).ToList();
            

            _taskManager._actList.Clear();
            _taskManager.Add(null, delegate { DatagridRefresh(currentEntries); });

        }

        /// <summary> Запись буфера обмена в строку поиска и установка курсора туда </summary>
        private void Window_Activated(object sender, EventArgs e)
        {
            SearchText.Text = Clipboard.GetText();
            SearchText.Focus();
        }

        /// <summary> обработка сортировки путём клика на заголовок таблицы </summary>
        private void DataGridHeader_Click(object sender, RoutedEventArgs e)
        {
            switch (((DataGridColumnHeader) sender).Content.ToString())
            {
                case "English":
                    SortDirection = "English";
                    break;
                case "Русский":
                    SortDirection = "Русский";
                    break;
                default:
                    MessageBox.Show("Колонку опознать не удалось");
                    break;
            }


            SortEntList(ref Entries);
            SearchText_TextChanged(SearchText, null);

        }

        /// <summary> очистка поля поиска двойным счелчком </summary>
        private void SearchText_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SearchText.Text = "";
        }

        /// <summary> очистка поля поиска кнопкой Х </summary>
        private void Xbutton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.Clear();
            SearchText.Text = "";

        }

        /// <summary> обработка удаления статьи клавишей Delete </summary>
        private void VocabData_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                var rowIndex = ((DataGrid)sender).SelectedIndex;
                Entries.RemoveAt(rowIndex);

                SaveXml(Entries, DictPath);

                LoadShowDict(DictPath);
            }
        }

        


    }

    public class Ent
    {
        public string Rus { get; set; }

        public string Eng { get; set; }
    }

}