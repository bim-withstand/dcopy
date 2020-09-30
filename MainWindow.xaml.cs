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
using System.Windows.Forms;
using System.ComponentModel;
using System.IO;
using Microsoft.Win32;
using System.Threading;
using System.Security.Cryptography;

namespace dcopy
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Init();
            this.DataContext = Global.MVM;
        }

        public void Init()
        {
            Global.regpath = Registry.CurrentUser.OpenSubKey("Software").OpenSubKey("STAND").OpenSubKey("dcopy", true);
            string source;
            string target;
            string sremove;

            if (Global.regpath != null)
            {
                source = Global.regpath.GetValue("Source").ToString();
                target = Global.regpath.GetValue("Target").ToString();
                sremove = Global.regpath.GetValue("Remove").ToString();

                if (source != null && source != "") { Global.MVM.source = source; }
                else { Global.MVM.source = ""; }

                if (target != null && target != "") { Global.MVM.target = target; }
                else { Global.MVM.target = ""; }

                if (sremove != null && sremove != "") { Global.MVM.remove = Convert.ToBoolean(sremove); }
                else { Global.MVM.remove = false; }
            }
            else if (Global.regpath == null)
            {
                Global.regpath = Registry.CurrentUser.OpenSubKey("Software", true).CreateSubKey("STAND").CreateSubKey("dcopy");
                Global.regpath.SetValue("Source", "");
                Global.regpath.SetValue("Target", "");
                Global.regpath.SetValue("Remove", "0");
            }
            // Global.regpath.Close();

            Global.MVM.status = "";
            Global.MVM.prepare_visibility = Visibility.Visible;
            Global.MVM.cancel_visibility = Visibility.Hidden;
        }

        public class Global
        {
            public static RegistryKey regpath;
            public static MainViewModel MVM = new MainViewModel();
            public static double totalSize;
            public static bool cts = false;
            public static BackgroundWorker prepareWorker;
            public static BackgroundWorker mainWorker;

        }

        public class MainViewModel : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged(string propertyName)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            }

            private string _source; public string source { get { return _source; } set { _source = value; OnPropertyChanged("source"); } }
            private string _target; public string target { get { return _target; } set { _target = value; OnPropertyChanged("target"); } }
            private string _status; public string status { get { return _status; } set { _status = value; OnPropertyChanged("status"); } }
            private bool _remove; public bool remove { get { return _remove; } set { _remove = value; OnPropertyChanged("remove"); } }
            private int _pbar_value; public int pbar_value { get { return _pbar_value; } set { _pbar_value = value; OnPropertyChanged("pbar_value"); } }
            private int _gpbar_value; public int gpbar_value { get { return _gpbar_value; } set { _gpbar_value = value; OnPropertyChanged("gpbar_value"); } }
            private List<item> _sourceList; public List<item> sourceList { get { return _sourceList; } set { _sourceList = value; OnPropertyChanged("sourceList"); } }
            private List<item> _targetList; public List<item> targetList { get { return _targetList; } set { _targetList = value; OnPropertyChanged("targetList"); } }
            private List<item> _copyList; public List<item> copyList { get { return _copyList; } set { _copyList = value; OnPropertyChanged("copyList"); } }
            private List<item> _removeList; public List<item> removeList { get { return _removeList; } set { _removeList = value; OnPropertyChanged("removeList"); } }
            private List<item> _modList; public List<item> modList { get { return _modList; } set { _modList = value; OnPropertyChanged("modList"); } }
            private Visibility _prepare_visibility; public Visibility prepare_visibility { get { return _prepare_visibility; } set { _prepare_visibility = value; OnPropertyChanged("prepare_visibility"); } }
            private Visibility _cancel_visibility; public Visibility cancel_visibility { get { return _cancel_visibility; } set { _cancel_visibility = value; OnPropertyChanged("cancel_visibility"); } }
            private Visibility _pbar_visible; public Visibility pbar_visible { get { return _pbar_visible; } set { _pbar_visible = value; OnPropertyChanged("pbar_visible"); } }

        }

        public class item
        {
            public bool modify { get; set; }
            public string action { get; set; }
            public string path;
            public string name { get; set; }
            public string date { get; set; }
            public int isize; // { get; set; }
            public string size { get; set; }
            public FileAttributes attributes;// { get; set; }
            public bool isDirectory;

            public item(string act, string fn, string fp, string dt, int fs, FileAttributes attrib, bool isDir)
            {
                this.modify = true;
                this.action = act;
                this.name = fn;
                this.path = fp;
                this.date = dt;
                this.isize = fs;
                this.isDirectory = isDir;
                this.size = Math.Round((double)this.isize / 1024, 0).ToString() + " kb";
                this.attributes = attrib;
            }

            public byte[] hash()
            {
                try
                {
                    using (var md5 = MD5.Create())
                    {
                        using (var stream = File.OpenRead(path))
                        {
                            return md5.ComputeHash(stream);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(ex.ToString());
                    return null;
                }

            }

        }

        public class fileList
        {
            public List<item> flist = new List<item>();

            public fileList(string path)
            {
                DirSearch(path, path);
            }

            private void DirSearch(string path, string rootPath)
            {
                DirectoryInfo pathInfo = new DirectoryInfo(path);

                try
                {
                    foreach (FileInfo File in pathInfo.GetFiles())
                    {
                        if (!Global.cts & !Global.prepareWorker.CancellationPending)
                        {
                            flist.Add(new item("", File.FullName.Replace(rootPath, ""), File.FullName, File.LastWriteTime.ToString("HH:mm:ss dd.MM.yyy"), (int)File.Length, File.Attributes, false));
                            Global.MVM.status = File.FullName.Replace(rootPath, "");
                        }
                    }
                    foreach (DirectoryInfo Directory in pathInfo.GetDirectories())
                    {
                        if (!Global.cts & !Global.prepareWorker.CancellationPending)
                        {
                            flist.Add(new item("", Directory.FullName.Replace(rootPath, ""), Directory.FullName, Directory.LastWriteTime.ToString("HH:mm:ss dd.MM.yyy"), 0, Directory.Attributes, true));
                            DirSearch(Directory.FullName, rootPath);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Global.MVM.status = ex.Message;
                }
            }
        }

        private void sourceFolder_Click(object sender, RoutedEventArgs e)
        {
            Global.MVM.status = "Select source folder...";
            var dialog = new FolderBrowserDialog();
            dialog.ShowNewFolderButton = false;
            dialog.SelectedPath = Global.MVM.source;
            DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                Global.MVM.source = dialog.SelectedPath.ToString();
                Global.MVM.status = "Source folder selected!";
            }
            else if (result == System.Windows.Forms.DialogResult.Cancel)
            {
                if (Global.MVM.source != "")
                {
                    Global.MVM.status = "Previous source folder preserved!";
                }
                else
                {
                    Global.MVM.status = "Source folder not selected!";
                }
            }
        }

        private void targetFolder_Click(object sender, RoutedEventArgs e)
        {
            Global.MVM.status = "Select target folder...";
            var dialog = new FolderBrowserDialog();
            dialog.SelectedPath = Global.MVM.target;
            DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                Global.MVM.target = dialog.SelectedPath.ToString();
                Global.MVM.status = "Target folder selected!";
            }
            else if (result == System.Windows.Forms.DialogResult.Cancel)
            {
                if (Global.MVM.target != "")
                {
                    Global.MVM.status = "Previous target folder preserved!";
                }
                else
                {
                    Global.MVM.status = "Target folder not selected!";
                }
            }
        }

        public List<item> diff(List<item> slist, List<item> tlist)
        {
            List<item> clist = new List<item>();

            if (!Global.prepareWorker.CancellationPending)
            {
                foreach (item s in slist)
                {
                    item currentTarget = tlist.Find(t => t.name == s.name);

                    //if (currentTarget.action == "")
                    //{
                        if (!s.isDirectory)
                        {
                            if (currentTarget == null)
                            {
                                s.action = "Copy";
                                clist.Add(s);
                            }
                            else if (currentTarget.action == "" && !currentTarget.hash().SequenceEqual(s.hash()))
                            {
                                s.action = "Replace";
                                clist.Add(s);
                            }
                        }
                        else
                        {
                            if (currentTarget == null)
                            {
                                s.action = "Copy";
                                clist.Add(s);
                            }
                        }
                    //}
                }

            }
            return clist;
        }

        private void prepare_Click(object sender, RoutedEventArgs e)
        {
            prepare.Visibility = Visibility.Hidden;
            cancel.Visibility = Visibility.Visible;
            Global.regpath = Registry.CurrentUser.OpenSubKey("Software").OpenSubKey("STAND").OpenSubKey("dcopy", true);
            Global.regpath.SetValue("Source", Global.MVM.source.ToString());
            Global.regpath.SetValue("Target", Global.MVM.target.ToString());
            Global.regpath.SetValue("Remove", Convert.ToString(Global.MVM.remove));
            Global.regpath.Close();
            Global.MVM.status = "Fetching item list...";

            if (Global.MVM.source != null || Global.MVM.target != null)
            {
                Global.cts = false;
                Global.prepareWorker = null;
                Global.MVM.sourceList = null;
                Global.MVM.targetList = null;
                Global.prepareWorker = new BackgroundWorker();
                Global.prepareWorker.WorkerReportsProgress = true;
                Global.prepareWorker.WorkerSupportsCancellation = true;
                Global.prepareWorker.DoWork += PrepareWorker_DoWork;
                Global.prepareWorker.RunWorkerCompleted += PrepareWorker_RunWorkerCompleted;
                Global.prepareWorker.RunWorkerAsync();
            }
            else
            {
                Global.MVM.status = "Please select a valid source and target!";
            }
        }

        private void PrepareWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Global.MVM.sourceList = new fileList(Global.MVM.source).flist;
            Global.MVM.targetList = new fileList(Global.MVM.target).flist;

            Global.MVM.copyList = diff(Global.MVM.sourceList, Global.MVM.targetList);
            Global.MVM.removeList = diff(Global.MVM.targetList, Global.MVM.sourceList);

            //Global.MVM.copyList.ForEach(x => x.action = "Copy");
            Global.MVM.removeList.ForEach(x => x.action = "Remove");

            Global.MVM.modList = new List<item>();
            if (Global.MVM.copyList.Count != 0) { Global.MVM.modList.AddRange(Global.MVM.copyList); }
            if (Global.MVM.removeList.Count != 0) { Global.MVM.modList.AddRange(Global.MVM.removeList); }

            string total = "";
            Global.totalSize = 0;
            foreach (item f in Global.MVM.copyList)
            {
                Global.totalSize += f.isize;
            }
            double tb = Math.Pow(1024, 4);
            double gb = Math.Pow(1024, 3);
            double mb = Math.Pow(1024, 2);
            double kb = Math.Pow(1024, 1);

            if (Global.totalSize > tb) { total = Math.Round(Global.totalSize / tb, 1).ToString() + " Tb"; }
            else if (Global.totalSize > gb) { total = Math.Round(Global.totalSize / gb, 1).ToString() + " Gb"; }
            else if (Global.totalSize > mb) { total = Math.Round(Global.totalSize / mb, 1).ToString() + " Mb"; }
            else if (Global.totalSize > kb) { total = Math.Round(Global.totalSize / kb, 1).ToString() + " Kb"; }
            else { total = Global.totalSize.ToString() + " Byte"; }
            Global.MVM.status = "Ready to copy " + total + " of " + Global.MVM.copyList.Count.ToString() + " files";
            Global.cts = false;
        }

        private void PrepareWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            prepare.Visibility = Visibility.Visible;
            cancel.Visibility = Visibility.Hidden;
            Global.prepareWorker.Dispose();
        }

        private void cancel_Click(object sender, RoutedEventArgs e)
        {
            Global.cts = true;

            if (Global.prepareWorker != null)
            {
                Global.prepareWorker.CancelAsync();
                Global.prepareWorker.Dispose();
            }
            if (Global.mainWorker != null)
            {
                Global.mainWorker.CancelAsync();
                Global.mainWorker.Dispose();
            }
            Global.MVM.sourceList = null;
            Global.MVM.targetList = null;
            Global.MVM.status = "Canceled";
            //Global.MVM.prepare_visibility = Visibility.Visible;
            //Global.MVM.cancel_visibility = Visibility.Hidden;
            prepare.Visibility = Visibility.Visible;
            cancel.Visibility = Visibility.Hidden;
        }

        private void update_Click(object sender, RoutedEventArgs e)
        {
            //Global.MVM.prepare_visibility = Visibility.Hidden;
            //Global.MVM.cancel_visibility = Visibility.Visible;
            prepare.Visibility = Visibility.Hidden;
            cancel.Visibility = Visibility.Visible;

            if (Global.MVM.sourceList != null || Global.MVM.targetList != null)
            {
                Global.mainWorker = new BackgroundWorker();
                Global.mainWorker.WorkerReportsProgress = true;
                Global.mainWorker.WorkerSupportsCancellation = true;
                Global.mainWorker.DoWork += MainWorker_DoWork; ;
                Global.mainWorker.RunWorkerCompleted += MainWorker_RunWorkerCompleted;
                Global.mainWorker.Disposed += MainWorker_Disposed;
                Global.mainWorker.RunWorkerAsync();
            }
            else
            {
                Global.MVM.status = "Please prepare the list first!";
            }
        }

        private void MainWorker_Disposed(object sender, EventArgs e)
        {
            Global.MVM.prepare_visibility = Visibility.Visible;
            Global.MVM.cancel_visibility = Visibility.Hidden;
        }

        private void MainWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Global.cts = false;
            removeFiles();
            copyFiles();
            Global.MVM.prepare_visibility = Visibility.Visible;
            Global.MVM.cancel_visibility = Visibility.Hidden;
            //prepare.Visibility = Visibility.Visible;
            //cancel.Visibility = Visibility.Hidden;
            if (Global.cts == false) { Global.MVM.status = "Done!"; }
            else { Global.MVM.status = "Canceled!"; }
        }

        private void MainWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Global.MVM.prepare_visibility = Visibility.Visible;
            Global.MVM.cancel_visibility = Visibility.Hidden;
            Global.mainWorker.Dispose();
        }

        private async void copyFiles()
        {
            Global.MVM.status = "Copying files...";
            Global.MVM.pbar_visible = Visibility.Visible;
            Global.MVM.pbar_value = 0;
            double copiedSize = 0;

            foreach (item f in Global.MVM.copyList)
            {
                if (Global.cts == false)
                {
                    string sourceFile = Global.MVM.source + f.name;
                    string targetFile = Global.MVM.target + f.name;
                    if (f.modify)
                    {
                        if (f.isDirectory)
                        {
                            string targetDir = Global.MVM.target + f.name;
                            if (!Directory.Exists(targetDir))
                            {
                                Directory.CreateDirectory(targetDir);
                                f.action = "Copied";
                            }
                        }
                        else
                        {
                            try
                            {
                                int bufferSize = 1000;
                                var buffer = new byte[bufferSize];

                                using (FileStream sourceStream = File.Open(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                {
                                    using (FileStream destinationStream = File.Create(targetFile))
                                    {
                                        int bytesRead = 0;
                                        int totalRead = 0;
                                        while ((bytesRead = await Task<int>.Factory.FromAsync(sourceStream.BeginRead, sourceStream.EndRead, buffer, 0, bufferSize, null)) > 0)
                                        {
                                            await Task.Factory.FromAsync(destinationStream.BeginWrite, destinationStream.EndWrite, buffer, 0, bytesRead, null);
                                            totalRead += bytesRead;
                                            copiedSize += (double)bytesRead;

                                            Global.MVM.pbar_value = (int)Math.Round(((double)totalRead) / (double)sourceStream.Length * 100, 0);
                                            Global.MVM.gpbar_value = (int)Math.Round((copiedSize) / Global.totalSize * 100, 0);

                                            if (Global.cts == false)
                                            {
                                                Global.MVM.status = Global.MVM.gpbar_value.ToString() + "% " + (totalRead / 1024) + " kb " + f.name;
                                            }
                                            else
                                            {
                                                Global.MVM.status = Global.MVM.gpbar_value.ToString() + "% " + (totalRead / 1024) + " kb Finishing!" + f.name;
                                            }
                                        }
                                    }
                                }
                                Global.MVM.pbar_value = 0;
                                File.SetLastWriteTime(targetFile, File.GetLastWriteTime(sourceFile));
                                File.SetAttributes(targetFile, File.GetAttributes(sourceFile));
                                f.action = "Copied";
                                //this.DataContext = null;
                                //this.DataContext = Global.MVM;

                                //Global.MVM.copyList.Remove(f);
                            }
                            catch (Exception ex)
                            {
                                Global.MVM.status = "Error: " + ex.Message;
                            }
                        }
                    }
                }
            }
            Global.MVM.pbar_visible = Visibility.Hidden;
            Global.MVM.prepare_visibility = Visibility.Visible;
            Global.MVM.cancel_visibility = Visibility.Hidden;
            if (Global.cts == false) { Global.MVM.status = "Done!"; }
            else { Global.MVM.status = "Canceled!"; }
        }

        private void removeFiles()
        {
            Global.MVM.status = "Removing files...";
            Global.MVM.pbar_visible = Visibility.Visible;
            Global.MVM.pbar_value = 0;
            int i = 0;
            Global.MVM.removeList.OrderBy(o => o.attributes).ToList();

            if (Global.MVM.remove)
            {
                foreach (item f in Global.MVM.removeList)
                {
                    File.SetAttributes(Global.MVM.target + f.name, FileAttributes.Archive);
                }
                foreach (item f in Global.MVM.removeList)
                {
                    if(f.modify)
                    {
                        string target = Global.MVM.target + f.name;
                        Global.MVM.status = "Removing " + target;
                        if (f.attributes.ToString().Split(',')[0] == "Directory" & Directory.Exists(target))
                        {
                            Directory.Delete(target, true);  //.Delete(targetFile);
                        }
                        else if (File.Exists(target))
                        {
                            File.Delete(target);
                        }
                        Global.MVM.pbar_value = (int)Math.Round(((double)i++) / (double)Global.MVM.removeList.Count * 100, 0);
                        f.action = "Removed";
                        //this.DataContext = null;
                        //this.DataContext = Global.MVM;
                    }
                }
            }
        }

        private async void removeFilesAsync()
        {
            Global.MVM.status = "Removing files...";
            if (Global.MVM.remove)
            {
                foreach (item f in Global.MVM.removeList)
                {
                    string targetFile = Global.MVM.target + f.name;
                    FileInfo fi = new FileInfo(targetFile);
                    await fi.DeleteAsync(); // C# 5
                }

            }
        }
    }
    public static class FileExtensions
    {
        public static Task DeleteAsync(this FileInfo fi)
        {
            return Task.Factory.StartNew(() => fi.Delete());
        }
    }
}
