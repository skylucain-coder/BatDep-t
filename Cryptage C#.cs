using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Drawing;


namespace BluetoothMessageApp
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Demande le numéro du propriétaire
            Console.Write("Entrez votre numéro de téléphone (ou adresse Bluetooth) : ");
            string proprietaire = Console.ReadLine();

            // Demande la clé secrète
            Console.Write("Entrez votre clé secrète : ");
            string cleSecrete = Console.ReadLine();

            // Créer un objet BluetoothClient
            BluetoothClient client = new BluetoothClient();

            // Recherche les appareils Bluetooth à proximité
            BluetoothDeviceInfo[] devices = client.DiscoverDevices();

            // Afficher les appareils trouvés
            Console.WriteLine("Appareils Bluetooth trouvés :");
            foreach (BluetoothDeviceInfo device in devices)
            {
                Console.WriteLine(device.DeviceName + " (" + device.DeviceAddress + ")");
            }

            // Demander le numéro / adresse du destinataire
            Console.Write("Entrez l'adresse Bluetooth du destinataire : ");
            string destinataire = Console.ReadLine();

            // Envoyer un message au destinataire (exemple console)
            Console.Write("Entrez un message : ");
            string message = Console.ReadLine();
            string messageCrypte = Cryptage(message, cleSecrete);
            EnvoyerMessage(client, destinataire, messageCrypte);

            // Thread pour recevoir des messages
            Thread thread = new Thread(() =>
            {
                RecevoirMessage(cleSecrete);
            });
            thread.IsBackground = true;
            thread.Start();

            // Créer un formulaire pour envoyer des messages
            Form form = new Form
            {
                Text = "Messenger",
                Width = 400,
                Height = 300
            };

            Label labelIP = new Label
            {
                Location = new Point(10, 10),
                Text = "Adresse IP :"
            };
            form.Controls.Add(labelIP);

            TextBox textBoxIp = new TextBox
            {
                Location = new Point(100, 10),
                Width = 200,
                Height = 20
            };
            form.Controls.Add(textBoxIp);

            Label labelPort = new Label
            {
                Location = new Point(10, 40),
                Text = "Port :"
            };
            form.Controls.Add(labelPort);

            TextBox textBoxPort = new TextBox
            {
                Location = new Point(100, 40),
                Width = 200,
                Height = 20
            };
            form.Controls.Add(textBoxPort);

            Button buttonConnect = new Button
            {
                Location = new Point(220, 70),
                Text = "Se connecter"
            };
            buttonConnect.Click += (sender, e) =>
            {
                string ip = textBoxIp.Text;
                if (int.TryParse(textBoxPort.Text, out int port))
                {
                    Connect(ip, port);
                }
                else
                {
                    MessageBox.Show("Port invalide");
                }
            };
            form.Controls.Add(buttonConnect);

            TextBox textBoxMessage = new TextBox
            {
                Location = new Point(10, 100),
                Width = 200,
                Height = 20
            };
            form.Controls.Add(textBoxMessage);

            Button buttonSend = new Button
            {
                Location = new Point(220, 100),
                Text = "Envoyer"
            };
            buttonSend.Click += (sender, e) =>
            {
                string messageToSend = textBoxMessage.Text;
                string messageCrypteToSend = Cryptage(messageToSend, cleSecrete);
                EnvoyerMessage(client, destinataire, messageCrypteToSend);
            };
            form.Controls.Add(buttonSend);

            Application.Run(form);
        }

        static byte[] DeriveKey(string cleSecrete)
        {
            // Dérive une clé 256 bits depuis la passphrase (SHA256)
            using (SHA256 sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(cleSecrete));
            }
        }

        static string Cryptage(string message, string cleSecrete)
        {
            byte[] key = DeriveKey(cleSecrete);
            byte[] iv = new byte[16]; // vecteur d'initialisation à zéro (pour démo)
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (StreamWriter sw = new StreamWriter(cs, Encoding.UTF8))
                    {
                        sw.Write(message);
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        static string Decryptage(string messageCrypte, string cleSecrete)
        {
            byte[] key = DeriveKey(cleSecrete);
            byte[] iv = new byte[16];
            byte[] buffer = Convert.FromBase64String(messageCrypte);
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using (MemoryStream ms = new MemoryStream(buffer))
                using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (StreamReader sr = new StreamReader(cs, Encoding.UTF8))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        static void EnvoyerMessage(BluetoothClient client, string destinataire, string messageCrypte)
        {
            try
            {
                BluetoothAddress address = BluetoothAddress.Parse(destinataire);
                // Utilise le service SerialPort pour la connexion Bluetooth
                client.Connect(address, BluetoothService.SerialPort);
                using (Stream stream = client.GetStream())
                {
                    byte[] data = Encoding.UTF8.GetBytes(messageCrypte);
                    stream.Write(data, 0, data.Length);
                }
                client.Close();
                Console.WriteLine("Message envoyé à " + destinataire);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur en envoyant le message : " + ex.Message);
            }
        }

        static void RecevoirMessage(string cleSecrete)
        {
            try
            {
                BluetoothListener listener = new BluetoothListener(BluetoothService.SerialPort);
                listener.Start();
                while (true)
                {
                    BluetoothClient connectedClient = listener.AcceptBluetoothClient();
                    using (Stream stream = connectedClient.GetStream())
                    {
                        byte[] data = new byte[4096];
                        int bytesRead = stream.Read(data, 0, data.Length);
                        if (bytesRead > 0)
                        {
                            string messageRecu = Encoding.UTF8.GetString(data, 0, bytesRead);
                            string messageDecrypte = Decryptage(messageRecu, cleSecrete);
                            Console.WriteLine("Message reçu : " + messageDecrypte);
                        }
                    }
                    connectedClient.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur réception Bluetooth : " + ex.Message);
            }
        }

        static void Connect(string ip, int port)
        {
            try
            {
                TcpClient client = new TcpClient();
                client.Connect(ip, port);
                Console.WriteLine("Connecté à " + ip + ":" + port);
                client.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur de connexion : " + ex.Message);
            }
        }
    }
}                   