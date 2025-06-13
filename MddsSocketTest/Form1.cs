using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
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
                if (!File.Exists(configPath))
                {
                    MessageBox.Show($"Config file not found at {configPath}. Please provide the correct path.");
                    Console.WriteLine("Debug: Config not loaded due to missing file.");
                }
                else
                {
                    Specs.LoadConfig(configPath); // Tải cấu hình từ config-test.xml
                    Console.WriteLine("Debug: Config loaded successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug: Error loading config: {ex.Message}");
            }
        }

        private async Task _DoSending(string id, string port, string path)
        {
            using (UdpClient socket = new UdpClient(id, int.Parse(port))) // Sử dụng using để quản lý tài nguyên
            {
                var lastSentTime = DateTime.Now;
                var seq = 0;

                using (StreamReader reader = new StreamReader(path))
                {
                    string line;
                    var errors = new List<string>();
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (_StopLoad)
                        {
                            break;
                        }
                        if (!string.IsNullOrEmpty(line))
                        {
                            var parseResult = new Msg(null, line)?.Parse(line, errors ?? new List<string>());
                            if (parseResult != null && socket != null)
                            {
                                // Sử dụng UnparsedText làm Raw và thêm sequence
                                string rawData = parseResult.UnparsedText ?? string.Empty;
                                byte[] buff = Encoding.ASCII.GetBytes(rawData);
                                socket.Send(buff, buff.Length);
                                seq++; // Tăng sequence
                                Console.WriteLine($"Debug: Sent message {seq} to {id}:{port}, length: {buff.Length}");
                            }
                            else
                            {
                                Console.WriteLine($"Debug: Failed to parse line: {string.Join(", ", errors ?? new List<string>())}, line: {line.Substring(0, Math.Min(50, line.Length))}");
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
                            // await Task.Delay(50); // Comment theo mẫu
                        }
                    }
                }
            }
        }

        private void btnStartSending_Click(object sender, EventArgs e)
        {
            _StopLoad = false;
            string rp = "C:\\Users\\Admin\\Downloads\\raw_mdds\\hsx\\";

            try
            {
                string configPath = @"C:\Users\Admin\MarketDataPluginSolution\MddsSocketTest\config\config-test.xml";
                if (!File.Exists(configPath))
                {
                    MessageBox.Show($"Config file not found at {configPath}. Please provide the correct path.");
                    return;
                }
                Specs.LoadConfig(configPath); // Đảm bảo cấu hình được tải
                XDocument doc = XDocument.Load(configPath);
                var groups = doc.Descendants("Group");
                foreach (var group in groups)
                {
                    var receivers = group.Elements("Receiver");
                    foreach (var receiver in receivers)
                    {
                        string mcGroup = receiver.Attribute("MulticastGroup")?.Value ?? string.Empty;
                        string port = receiver.Attribute("Port")?.Value ?? string.Empty;
                        string dataKey = receiver.Attribute("DataKey")?.Value ?? string.Empty;
                        string dataPath = Path.Combine(rp, dataKey + ".raw");
                        if (string.IsNullOrEmpty(mcGroup) || !File.Exists(dataPath))
                            continue;

                        _ = Task.Run(async () =>
                        {
                            await _DoSending(mcGroup, port, dataPath);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Debug: Error loading config or sending: {ex.Message}");
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _StopLoad = true;
            Console.WriteLine("Debug: Stop signal sent");
        }
    }
}