﻿using Communication;
using Communication.Interfaces;
using Parser.Interfaces;
using System.Collections.Concurrent;
using TopPortLib.Interfaces;

namespace TopPortLib
{
    /// <summary>
    /// 顶层通讯口
    /// </summary>
    public class TopPort_Server : ITopPort_Server, IDisposable
    {
        private readonly ConcurrentDictionary<int, IParser> _dicParsers = new();

        /// <inheritdoc/>
        public IPhysicalPort_Server PhysicalPort { get; }

        /// <inheritdoc/>
        public event ReceiveParsedDataFromClientEventHandler? OnReceiveParsedData;
        /// <inheritdoc/>
        public event ClientConnectEventHandler? OnClientConnect;
        /// <inheritdoc/>
        public event ClientDisconnectEventHandler? OnClientDisconnect;

        /// <summary>
        /// 顶层通讯口
        /// </summary>
        /// <param name="physicalPort">物理口</param>
        /// <param name="getParser">获取解析器</param>
        public TopPort_Server(IPhysicalPort_Server physicalPort, GetParserEventHandler getParser)
        {
            PhysicalPort = physicalPort;
            PhysicalPort.OnReceiveOriginalDataFromClient += async (byte[] data, int size, int clientId) =>
            {
                if (_dicParsers.ContainsKey(clientId))
                {
                    if (_dicParsers.TryGetValue(clientId, out var parser))
                        await parser.ReceiveOriginalDataAsync(data, size);
                }
            };
            PhysicalPort.OnClientConnect += async clientId =>
            {
                var parser = await getParser.Invoke();
                parser.OnReceiveParsedData += async data =>
                {
                    if (OnReceiveParsedData is not null) await OnReceiveParsedData.Invoke(clientId, data);
                };
                _dicParsers.TryAdd(clientId, parser);
                if (OnClientConnect is not null) await OnClientConnect.Invoke(clientId);
            };
            PhysicalPort.OnClientDisconnect += async clientId =>
            {
                if (_dicParsers.TryRemove(clientId, out var parser))
                {
                    if (parser is IDisposable needDisposingParser)
                    {
                        needDisposingParser.Dispose();
                    }
                }
                if (OnClientDisconnect is not null) await OnClientDisconnect.Invoke(clientId);
            };
        }

        /// <inheritdoc/>
        public async Task CloseAsync()
        {
            await PhysicalPort.StopAsync();
        }

        /// <inheritdoc/>
        public async Task OpenAsync()
        {
            await PhysicalPort.StartAsync();
        }

        /// <inheritdoc/>
        public async Task SendAsync(int clientId, byte[] data)
        {
            await PhysicalPort.SendDataAsync(clientId, data);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            var task = this.CloseAsync();
            task.ConfigureAwait(false);
            task.Wait();
            GC.SuppressFinalize(this);
        }
    }
}
