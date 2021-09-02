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
using System.Management;
using System.ServiceProcess;
using System.Diagnostics;
using System.ComponentModel;

namespace WindowsServiceManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ManagementScope scope;
        string servername;
        bool connected = false;
        bool checkboxAllChecked = false;
        bool checkboxDisabledChecked = false;
        List<ServiceModel> servicelist = new List<ServiceModel>();

        public MainWindow()
        {
            InitializeComponent();
            EnableDisableControls();
        }

        protected void EnableDisableControls()
        {
            btnRefresh.IsEnabled = connected;
            checkboxAll.IsEnabled = connected;
            checkboxDisabled.IsEnabled = connected;
            btnStart.IsEnabled = connected;
            btnStop.IsEnabled = connected;
            btnRestart.IsEnabled = connected;
            btnStartAll.IsEnabled = connected;
            btnStopAll.IsEnabled = connected;
            btnRestartAll.IsEnabled = connected;
        }

        protected void GetServiceList()
        {
            servicelist.Clear();
            string query;

            if (checkboxAllChecked)
            {
                if (checkboxDisabledChecked)
                    query = "select * from Win32_Service";
                else
                    query = "select * from Win32_Service where StartMode <> 'Disabled'";
            }
            else
            {
                if (checkboxDisabledChecked)
                    query = "select * from Win32_Service where caption like '%Dynamics%'";
                else
                    query = "select * from Win32_Service where StartMode <> 'Disabled' and caption like '%Dynamics%'";
            }

            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query));
                ManagementObjectCollection retObjectCollection = searcher.Get();

                foreach (ManagementObject mo in retObjectCollection)
                {
                    servicelist.Add(new ServiceModel()
                    {
                        Caption = mo["Caption"].ToString(),
                        DisplayName = mo["DisplayName"].ToString(),
                        Name = mo["Name"].ToString(),
                        ProcessId = (uint)mo["ProcessId"],
                        Started = (bool)mo["Started"],
                        StartMode = mo["StartMode"].ToString(),
                        State = mo["State"].ToString(),
                        Status = mo["Status"].ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                return;
            }
        }

        protected void DisplayServices()
        {
            datagridServices.Items.Clear();
            foreach (ServiceModel serviceModel in servicelist)
                datagridServices.Items.Add(serviceModel);
        }

        protected void DisplayDataGrid(object sender, RunWorkerCompletedEventArgs e)
        {
            DisplayServices();
        }

        protected void ConnectDisconnect()
        {
            if (connected)
            {
                datagridServices.Items.Clear();
                connected = false;
                btnConnect.Content = "Connect";
                EnableDisableControls();
                return;
            }
            string Namespace = @"root\cimv2";
            servername = txtServerName.Text.ToString();
            string UserName = txtUserName.Text.ToString();
            string Password = txtPassword.Password.ToString();
            if (string.IsNullOrEmpty(servername))
                servername = System.Environment.MachineName.ToString();

            ConnectionOptions options = new ConnectionOptions();
            if (!string.IsNullOrEmpty(UserName))
                options.Username = UserName;
            if (!string.IsNullOrEmpty(Password))
                options.Password = Password;

            scope = new ManagementScope(string.Format(@"\\{0}\{1}", servername, Namespace), options);

            try
            {
                scope.Connect();
                connected = true;
                GetServiceList();
                DisplayServices();
                EnableDisableControls();
                btnConnect.Content = "Disconnect";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                return;
            }
        }

        protected void GenerateServiceList(object sender, DoWorkEventArgs e)
        {
            GetServiceList();
        }

        protected string StartService(string servicename)
        {
            ServiceController sc = new ServiceController(servicename, servername);

            try
            {
                if (sc.Status != ServiceControllerStatus.Stopped)
                {
                    MessageBox.Show("Service " + sc.DisplayName + " cannot be started or is already running");
                    return sc.Status.ToString();
                }
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running);
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            return sc.Status.ToString();            
        }

        protected void StartServices(object sender, DoWorkEventArgs e)
        {
            List<ServiceModel> servicelist = (List<ServiceModel>)e.Argument;
            foreach(ServiceModel service in servicelist)
            {
                string servicestatus = StartService(service.Name);
                service.State = servicestatus;
            }
            GetServiceList();
        }

        protected string StopService(string servicename)
        {
            var query = new SelectQuery("select ProcessId from Win32_Service where name = '" + servicename + "'");
            ServiceController sc = new ServiceController(servicename, servername);

            try
            {
                if (sc.Status == ServiceControllerStatus.Stopped)                                    
                    return sc.Status.ToString();                
                using (var searcher = new ManagementObjectSearcher(scope, query))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        uint processId = (uint)obj["ProcessId"];
                        var parentIdQuery = new SelectQuery("SELECT * FROM Win32_Process WHERE ProcessId = '" + processId + "'");
                        using (var vsearcher = new ManagementObjectSearcher(scope, parentIdQuery))
                        {
                            foreach (ManagementObject process in vsearcher.Get())
                            {
                                uint parentProcessId = (uint)process["ProcessId"];
                                if (processId == parentProcessId)
                                {
                                    uint r = (uint)process.InvokeMethod("Terminate", null);
                                    if (r != 0)
                                    {
                                        Process myprocess = new Process();
                                        myprocess = Process.GetProcessById((int)(processId), servername);
                                        myprocess.Kill();
                                    }
                                }
                            }
                        }
                        sc.WaitForStatus(ServiceControllerStatus.Stopped);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            return sc.Status.ToString();
        }

        protected void StopServices(object sender, DoWorkEventArgs e)
        {
            List<ServiceModel> servicelist = (List<ServiceModel>)e.Argument;
            foreach (ServiceModel service in servicelist)
            {
                string servicestatus = StopService(service.Name);
                service.State = servicestatus;
            }
            GetServiceList();
        }

        protected string RestartService(string servicename)
        {
            StopService(servicename);
            return (StartService(servicename));
        }

        protected void RestartServices(object sender, DoWorkEventArgs e)
        {
            List<ServiceModel> servicelist = (List<ServiceModel>)e.Argument;
            foreach (ServiceModel service in servicelist)
            {
                string servicestatus = RestartService(service.Name);
                service.State = servicestatus;
            }
            GetServiceList();
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            ConnectDisconnect();            
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += GenerateServiceList;
            backgroundWorker.RunWorkerCompleted += DisplayDataGrid;
            backgroundWorker.RunWorkerAsync();
        }

        private void checkboxAll_Changed(object sender, RoutedEventArgs e)
        {
            checkboxAllChecked = (bool)checkboxAll.IsChecked;
            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += GenerateServiceList;
            backgroundWorker.RunWorkerCompleted += DisplayDataGrid;
            backgroundWorker.RunWorkerAsync();

            btnStartAll.IsEnabled = !checkboxAllChecked;
            btnStopAll.IsEnabled = !checkboxAllChecked;
            btnRestartAll.IsEnabled = !checkboxAllChecked;
        }

        private void checkboxDisabled_Changed(object sender, RoutedEventArgs e)
        {
            checkboxDisabledChecked = (bool)checkboxDisabled.IsChecked;
            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += GenerateServiceList;
            backgroundWorker.RunWorkerCompleted += DisplayDataGrid;
            backgroundWorker.RunWorkerAsync();
        }
                
        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            List<ServiceModel> servicelist = new List<ServiceModel>();
            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += StartServices;
            backgroundWorker.RunWorkerCompleted += DisplayDataGrid;

            foreach (ServiceModel service in datagridServices.SelectedItems)
            {
                servicelist.Add(service);
            }
            backgroundWorker.RunWorkerAsync(servicelist);
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            List<ServiceModel> servicelist = new List<ServiceModel>();
            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += StopServices;
            backgroundWorker.RunWorkerCompleted += DisplayDataGrid;

            foreach (ServiceModel service in datagridServices.SelectedItems)
            {
                servicelist.Add(service);
            }
            backgroundWorker.RunWorkerAsync(servicelist);
        }

        private void btnRestart_Click(object sender, RoutedEventArgs e)
        {
            List<ServiceModel> servicelist = new List<ServiceModel>();
            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += RestartServices;
            backgroundWorker.RunWorkerCompleted += DisplayDataGrid;

            foreach (ServiceModel service in datagridServices.SelectedItems)
            {
                servicelist.Add(service);
            }
            backgroundWorker.RunWorkerAsync(servicelist);
        }

        private void btnStartAll_Click(object sender, RoutedEventArgs e)
        {
            List<ServiceModel> servicelist = new List<ServiceModel>();
            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += StartServices;
            backgroundWorker.RunWorkerCompleted += DisplayDataGrid;

            foreach (ServiceModel service in datagridServices.Items)
            {
                servicelist.Add(service);
            }
            backgroundWorker.RunWorkerAsync(servicelist);
        }

        private void btnStopAll_Click(object sender, RoutedEventArgs e)
        {
            List<ServiceModel> servicelist = new List<ServiceModel>();
            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += StopServices;
            backgroundWorker.RunWorkerCompleted += DisplayDataGrid;

            foreach (ServiceModel service in datagridServices.Items)
            {
                servicelist.Add(service);
            }
            backgroundWorker.RunWorkerAsync(servicelist);
        }

        private void btnRestartAll_Click(object sender, RoutedEventArgs e)
        {
            List<ServiceModel> servicelist = new List<ServiceModel>();
            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += RestartServices;
            backgroundWorker.RunWorkerCompleted += DisplayDataGrid;

            foreach (ServiceModel service in datagridServices.Items)
            {
                servicelist.Add(service);
            }
            backgroundWorker.RunWorkerAsync(servicelist);
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}