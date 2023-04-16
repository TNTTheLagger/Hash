using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Hash
{
    public class Node
    {
        private TcpListener _listener;
        private List<TcpClient> _peers;
        private Dictionary<string, string> _data;
        private string _sharedSecret;
        private int _port;
        private Timer _timer;

        public Node(string sharedSecret, int port)
        {
            _sharedSecret = sharedSecret;
            _port = port;
            _peers = new List<TcpClient>();
            _data = new Dictionary<string, string>();

            // Start listening for incoming connections
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _listener.BeginAcceptTcpClient(HandleIncomingConnection, null);

            // Start timer for data mirroring
            _timer = new Timer(MirrorData, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
        }

        public void Connect(string ipAddress)
        {
            // Connect to existing node in the network
            var client = new TcpClient();
            client.Connect(ipAddress, _port);
            _peers.Add(client);
        }

        public void Store(string id, string data)
        {
            // Encrypt the data using the shared secret
            var encryptedData = EncryptString(data, _sharedSecret);

            // Store the encrypted data in the local data dictionary
            _data[id] = encryptedData;

            // Broadcast the data to all connected peers
            BroadcastData($"STORE|{id}|{encryptedData}");
        }

        public string Get(string id)
        {
            // Check if the data is available locally
            if (_data.ContainsKey(id))
            {
                // Decrypt and return the data
                var encryptedData = _data[id];
                var decryptedData = DecryptString(encryptedData, _sharedSecret);
                return decryptedData;
            }

            // Broadcast a request for the data to all connected peers
            BroadcastData($"GET|{id}");

            // Wait for response from peers
            int attempts = 0;
            while (!_data.ContainsKey(id) && attempts < 3)
            {
                Thread.Sleep(1000);
                attempts++;
            }

            // If data is not available, return null
            if (!_data.ContainsKey(id))
                return null;

            // Decrypt and return the data
            var encryptedDataRemote = _data[id];
            var decryptedDataRemote = DecryptString(encryptedDataRemote, _sharedSecret);
            return decryptedDataRemote;
        }

        private void HandleIncomingConnection(IAsyncResult result)
        {
            // Accept incoming connection from a peer
            var client = _listener.EndAcceptTcpClient(result);
            _peers.Add(client);

            // Continue listening for incoming connections
            _listener.BeginAcceptTcpClient(HandleIncomingConnection, null);
        }

        private void BroadcastData(string data)
        {
            // Broadcast data to all connected peers
            var dataBytes = Encoding.UTF8.GetBytes(data);
            foreach (var peer in _peers.ToList())
            {
                try
                {
                    peer.GetStream().Write(dataBytes, 0, dataBytes.Length);
                }
                catch (IOException)
                {
// If an exception occurs, assume the peer has disconnected
                    _peers.Remove(peer);
                }
            }
        }

        private void MirrorData(object state)
        {
            // Mirror local data to all connected peers
            foreach (var peer in _peers.ToList())
            {
                try
                {
                    var data = string.Join(",", _data.Select(kv => $"{kv.Key}|{kv.Value}"));
                    var mirroredData = $"MIRROR|{data}";
                    var dataBytes = Encoding.UTF8.GetBytes(mirroredData);
                    peer.GetStream().Write(dataBytes, 0, dataBytes.Length);
                }
                catch (IOException)
                {
                    // If an exception occurs, assume the peer has disconnected
                    _peers.Remove(peer);
                }
            }
        }

        private string EncryptString(string data, string key)
        {
            // Encrypt a string using a key
            byte[] encryptedBytes;
            using (var aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(key);
                aes.GenerateIV();

                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    ms.Write(aes.IV, 0, aes.IV.Length);
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new StreamWriter(cs))
                    {
                        sw.Write(data);
                    }

                    encryptedBytes = ms.ToArray();
                }
            }

            return Convert.ToBase64String(encryptedBytes);
        }

        private string DecryptString(string encryptedData, string key)
        {
            // Decrypt an encrypted string using a key
            byte[] encryptedBytes = Convert.FromBase64String(encryptedData);
            using (var aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(key);
                byte[] iv = new byte[aes.IV.Length];
                Array.Copy(encryptedBytes, iv, iv.Length);
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(encryptedBytes, iv.Length, encryptedBytes.Length - iv.Length))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs))
                {
                    return sr.ReadToEnd();
                }
            }
        }
    }
}