using System;
using System.Windows;
using Microsoft.Win32;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Mavlink;
namespace Mavlink
{
  
    public class MessageInfoData
    { 
        public uint MsgId { get; set; }
        public string Name { get; set; }
        public byte Crc { get; set; }
        public uint MinLength { get; set; }
        public uint Length { get; set; }
        public Type Type { get; set; }
    }
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        } 
        private void OnAnalyzeButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "DLL files (*.dll)|*.dll",
                    Title = "Select MAVLink DLL"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var (content, messageInfos) = ExtractMavlinkInfo(openFileDialog.FileName);
                    SaveFiles(content, messageInfos);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private (string textContent, List<MessageInfoData> messageInfos) ExtractMavlinkInfo(string dllPath)
        {
            var assembly = Assembly.LoadFrom(dllPath);
            var result = new List<string>();
            var messageInfos = new List<MessageInfoData>();

            try
            {
                var types = assembly.GetTypes();
                var mavLinkType = types.FirstOrDefault(t => t.Name == "MAVLink");

                if (mavLinkType != null)
                {
                    var fields = mavLinkType.GetFields();
                    var messageInfoField = fields.FirstOrDefault(f => f.Name == "MAVLINK_MESSAGE_INFOS");

                    if (messageInfoField != null)
                    {
                        var fieldValue = messageInfoField.GetValue(null);

                        if (fieldValue is Array array)
                        {
                            foreach (var item in array)
                            {
                                var messageId = Convert.ToUInt32(item.GetType().GetProperty("msgid").GetValue(item));
                                var messageName = item.GetType().GetProperty("name").GetValue(item).ToString();
                                var messageCrc = Convert.ToByte(item.GetType().GetProperty("crc").GetValue(item));
                                var messageMinLength = Convert.ToUInt32(item.GetType().GetProperty("minlength").GetValue(item));
                                var messageLength = Convert.ToUInt32(item.GetType().GetProperty("length").GetValue(item));
                                var messageType = item.GetType().GetProperty("type")?.GetValue(item) as Type;
                                //MY_MAVLINK_MESSAGE_INFOS.Add(new message_info(messageId,messageName,messageCrc,messageMinLength,messageLength,messageType));
                                var messageInfo = new MessageInfoData
                                {
                                    MsgId = messageId,
                                    Name = messageName,
                                    Crc = messageCrc,
                                    MinLength = (uint)messageMinLength,
                                    Length = (uint)messageLength,
                                    Type = (Type)messageType
                                };
                                messageInfos.Add(messageInfo);

                                result.Add(
                                    $"MESSAGE ID: {messageId}\n" +
                                    $"MESSAGE NAME: {messageName}\n" +
                                    $"CRC: {messageCrc}\n" +
                                    $"MIN LENGTH: {messageMinLength}\n" +
                                    $"LENGTH: {messageLength}\n" +
                                    $"TYPE: {messageType}\n" +
                                    "------------------------"
                                );
                            }

                            // Add total message count at the beginning
                            result.Insert(0, $"Total Number of Messages: {messageInfos.Count}\n------------------------\n");
                        }
                    }
                }
                else
                {
                    MessageBox.Show("No MAVLink information found in this DLL!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return (string.Empty, messageInfos);
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                MessageBox.Show($"Error loading types: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return (string.Empty, messageInfos);
            }

            return (string.Join("\n", result), messageInfos);
        }

        private void SaveFiles(string content, List<MessageInfoData> messageInfos)
        {
            if (string.IsNullOrEmpty(content) || !messageInfos.Any())
            {
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt",
                Title = "Save MAVLink Info"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                // Save txt file
                string txtPath = saveFileDialog.FileName;
                File.WriteAllText(txtPath, content);

                // Save cs file
                string csPath = Path.ChangeExtension(txtPath, ".cs");
                SaveMessageInfosAsCs(csPath, messageInfos);

                MessageBox.Show($"MAVLink information saved successfully.\nText file: {txtPath}\nC# file: {csPath}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SaveMessageInfosAsCs(string path, List<MessageInfoData> messageInfos)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine("namespace MAVLink");
            sb.AppendLine("{");
            sb.AppendLine("    public class message_info");
            sb.AppendLine("    {");
            sb.AppendLine("        public int msgid { get; set; }");
            sb.AppendLine("        public string name { get; set; }");
            sb.AppendLine("        public byte crc { get; set; }");
            sb.AppendLine("        public int minlength { get; set; }");
            sb.AppendLine("        public int length { get; set; }");
            sb.AppendLine("        public Type type { get; set; }");
            sb.AppendLine();
            sb.AppendLine("        public message_info(int msgid, string name, byte crc, int minlength, int length, Type type)");
            sb.AppendLine("        {");
            sb.AppendLine("            this.msgid = msgid;");
            sb.AppendLine("            this.name = name;");
            sb.AppendLine("            this.crc = crc;");
            sb.AppendLine("            this.minlength = minlength;");
            sb.AppendLine("            this.length = length;");
            sb.AppendLine("            this.type = type;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public static class MAVLink");
            sb.AppendLine("    {");
            sb.AppendLine("        public static message_info[] MAVLINK_MESSAGE_INFOS = new message_info[]");
            sb.AppendLine("        {");

            // Add each message_info
            foreach (var info in messageInfos)
            {
                sb.AppendLine($"            new message_info({info.MsgId}, \"{info.Name}\", {info.Crc}, {info.MinLength}, {info.Length}, typeof({info.Type})),");
            }

            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(path, sb.ToString());
        }
    }
}