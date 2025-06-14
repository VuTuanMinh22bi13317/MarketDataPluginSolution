using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Collections.Generic;
using MarketDataPlugin;
using MarketDataPlugin.DEF;

namespace MddsSocketTest
{
    public partial class Form1 : Form
    {
        private bool _StopLoad = false;
        private bool _isReceiving = false; // Biến kiểm soát nhận dữ liệu
        private UdpClient _receiveClient; // Socket để nhận dữ liệu
        private readonly string _logFilePath = @"C:\Users\Admin\MarketDataPluginSolution\MddsSocketTest\logs\receiver_log.txt";

        public Form1()
        {
            InitializeComponent();
            txtSeq.Text = "0"; // Đảm bảo txtSeq được thêm vào form
            try
            {
                string configPath = @"C:\Users\Admin\MarketDataPluginSolution\MddsSocketTest\config\config-test.xml";
                if (File.Exists(configPath))
                {
                    Specs.LoadConfig(configPath); // Gọi phương thức tĩnh để tải cấu hình
                    Console.WriteLine("Debug: Config loaded successfully.");
                }
                else
                {
                    Console.WriteLine("Debug: Config file not found at " + configPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug: Error loading config: {ex.Message}");
            }
        }

        private async Task _DoSending(string id, string port, string path)
        {
            UdpClient socket = null;
            try
            {
                socket = new UdpClient(id, int.Parse(port)); // Mở socket với địa chỉ multicast
                Console.WriteLine($"Debug: Socket opened successfully on {id}:{port}");
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Debug: Failed to open socket on {id}:{port} - Error: {ex.Message}");
                return; // Thoát nếu không mở được socket
            }

            var lastSentTime = DateTime.Now;
            var seq = 0;
            using (StreamReader reader = new StreamReader(path))
            {
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (_StopLoad)
                    {
                        socket.Close();
                        Console.WriteLine($"Debug: Socket closed on {id}:{port} due to stop signal");
                        break;
                    }
                    if (!string.IsNullOrEmpty(line))
                    {
                        Specs.LoadConfig(@"C:\Users\Admin\MarketDataPluginSolution\MddsSocketTest\config\config-test.xml");
                        if (Specs.MsgDefs != null && Specs.MsgDefs.Count > 0)
                        {
                            var defaultMsgDef = Specs.MsgDefs.Values.First(); // Lấy MsgDef mặc định
                            var msg = new Msg(defaultMsgDef, line); // Tạo Msg
                            var errors = new List<string>();
                            var parseResult = msg.Parse(string.Empty, errors); // Parse dữ liệu
                            if (parseResult != null)
                            {
                                if (socket != null)
                                {
                                    seq++; // Tăng sequence mà không gán vào msg.MsgSeqNum
                                    byte[] buff = Encoding.ASCII.GetBytes(msg.Raw);
                                    try
                                    {
                                        socket.Send(buff, buff.Length);
                                        Console.WriteLine($"Debug: Data sent successfully on {id}:{port}, Seq: {seq}, Data: {msg.Raw}");
                                    }
                                    catch (SocketException ex)
                                    {
                                        Console.WriteLine($"Debug: Failed to send data on {id}:{port} - Error: {ex.Message}");
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Debug: Parse failed - Errors: {string.Join(", ", errors)}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Debug: MsgDefs is null or empty, skipping parse.");
                        }

                        if (txtSeq.InvokeRequired)
                        {
                            txtSeq.Invoke(new Action(() =>
                            {
                                txtSeq.Text = (int.Parse(txtSeq.Text) + 1).ToString();
                            }));
                        }
                        else
                        {
                            txtSeq.Text = (int.Parse(txtSeq.Text) + 1).ToString();
                        }
                    }
                    if (seq % 50 == 0)
                    {
                        await Task.Delay(50); // Giữ nguyên để bảo vệ máy của bạn
                    }
                }
            }
            if (socket != null)
            {
                socket.Close();
                Console.WriteLine($"Debug: Socket closed on {id}:{port} after sending");
            }
        }

        private void btnStartSending_Click(object sender, EventArgs e)
        {
            Console.WriteLine($"Debug: btnStartSending_Click triggered at {DateTime.Now}");
            _StopLoad = false;
            string rp = @"C:\Users\Admin\Downloads\raw_mdds\hsx\";
            //string rp = @"C:\Users\Admin\Downloads\raw_mdds\hnx\";

            XDocument doc = XDocument.Load(@"C:\Users\Admin\MarketDataPluginSolution\MddsSocketTest\config\config-test.xml");
            var groups = doc.Descendants("Group");
            foreach (var group in groups)
            {
                var receivers = group.Elements("Receiver");
                foreach (var receiver in receivers)
                {
                    string mcGroup = (string)receiver.Attribute("MulticastGroup");
                    string port = (string)receiver.Attribute("Port");
                    string dataPath = rp + (string)receiver.Attribute("DataKey") + ".raw";
                    if (receiver.Attribute("DataKey").ToString().Contains("GSTO000015"))
                    {
                    }
                    if (string.IsNullOrEmpty(mcGroup) || !File.Exists(dataPath))
                        continue;
                    Console.WriteLine($"Debug: Starting sending to {mcGroup}:{port} from {dataPath}");
                    _ = Task.Run(async () =>
                    {
                        await _DoSending(mcGroup, port, dataPath);
                    });
                }
            }
        }

        private async void btnStartReceiving_Click(object sender, EventArgs e)
        {
            if (_isReceiving) return;

            _isReceiving = true;
            // Lấy địa chỉ và cổng từ config-test.xml (giả sử dùng receiver đầu tiên)
            XDocument doc;
            try
            {
                doc = XDocument.Load(@"C:\Users\Admin\MarketDataPluginSolution\MddsSocketTest\config\config-test.xml");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug: Error loading config for receiving: {ex.Message}");
                return;
            }
            var receiver = doc.Descendants("Receiver").FirstOrDefault();
            if (receiver == null)
            {
                Console.WriteLine("Debug: No receiver configuration found.");
                return;
            }

            string multicastAddress = (string)receiver.Attribute("MulticastGroup");
            int port;
            try
            {
                port = int.Parse((string)receiver.Attribute("Port"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug: Invalid port in config: {ex.Message}");
                return;
            }

            try
            {
                _receiveClient = new UdpClient(port);
                _receiveClient.JoinMulticastGroup(IPAddress.Parse(multicastAddress));
                Console.WriteLine($"Debug: Started receiving on {multicastAddress}:{port}");

                while (_isReceiving)
                {
                    var result = await _receiveClient.ReceiveAsync();
                    string data = Encoding.ASCII.GetString(result.Buffer);
                    UpdateLog(data);

                    if (txtReceived.InvokeRequired)
                    {
                        txtReceived.Invoke(new Action(() => txtReceived.AppendText(data + Environment.NewLine)));
                    }
                    else
                    {
                        txtReceived.AppendText(data + Environment.NewLine);
                    }
                    Console.WriteLine($"Debug: Received data: {data}");
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Debug: Socket error while receiving on {multicastAddress}:{port} - {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug: Unexpected error while receiving - {ex.Message}");
            }
            finally
            {
                _receiveClient?.Close();
                Console.WriteLine($"Debug: Receiver closed on {multicastAddress}:{port}");
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            Console.WriteLine($"Debug: btnStop_Click triggered at {DateTime.Now}");
            _StopLoad = true; // Dừng gửi
            _isReceiving = false; // Dừng nhận
            _receiveClient?.Close(); // Đóng socket nhận nếu có
            Console.WriteLine("Debug: Stop signal sent");
        }

        private void UpdateLog(string data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath) ?? "logs");
                File.AppendAllText(_logFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {data}{Environment.NewLine}");
                Console.WriteLine($"Debug: Logged to {_logFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug: Error writing log - {ex.Message}");
            }
        }
    }
}