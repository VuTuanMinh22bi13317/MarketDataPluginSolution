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
                    Console.WriteLine("Debug: Config file not found.");
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
            // Cập nhật đường dẫn
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
                        // Tải cấu hình lại trong phương thức (nếu cần)
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
                                        Console.WriteLine($"Debug: Data sent successfully on {id}:{port}, Seq: {seq}");
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
                        await Task.Delay(50); // Comment vì không thấy delay trong yêu cầu
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
            //string rp = @"C:\Users\Admin\MarketDataPluginSolution\MddsSocketTest\raw_mdds\hnx\";

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
                    //
                    _ = Task.Run(async () =>
                    {
                        await _DoSending(mcGroup, port, dataPath);
                    });
                }
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            Console.WriteLine($"Debug: btnStop_Click triggered at {DateTime.Now}");
            _StopLoad = true;
            Console.WriteLine("Debug: Stop signal sent");
        }
    }
}