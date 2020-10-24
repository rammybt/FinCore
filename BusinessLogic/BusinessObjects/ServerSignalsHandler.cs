﻿using Autofac;
using BusinessLogic.Repo;
using BusinessObjects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace BusinessLogic.BusinessObjects
{
    public class ServerSignalsHandler : ISignalHandler
    {
        private readonly MainService xtrade;
        private readonly IWebLog log;
        //private readonly ITerminalEvents terminals;

        public ServerSignalsHandler()
        {
            xtrade = MainService.thisGlobal;
            //terminals = MainService.thisGlobal.Container.Resolve<ITerminalEvents>();
            log = MainService.thisGlobal.Container.Resolve<IWebLog>();
        }

        public SignalInfo ListenSignal(long flags, long objectId)
        {
            return xtrade.ListenSignal(flags, objectId);
        }

        public void PostSignal(SignalInfo signal, IMessagingServer server)
        {
            if ((SignalFlags)signal.Flags == SignalFlags.Cluster)
            {
                xtrade.PostSignalTo(signal);
                return;
            }

            switch ((EnumSignals)signal.Id)
            {
                case EnumSignals.SIGNAL_POST_LOG:
                    {
                        if (signal.Data == null)
                            break;
                        MainService.thisGlobal.DoLog(signal);
                    }
                    break;
                case EnumSignals.SIGNAL_CHECK_HEALTH:
                    if (xtrade.IsDebug())
                        log.Info("CheckHealth: " + signal.Flags);
                    break;
                case EnumSignals.SIGNAL_DEALS_HISTORY:
                    {
                        List<DealInfo> deals = null;
                        if (signal.Data != null)
                            deals = JsonConvert.DeserializeObject<List<DealInfo>>(signal.Data.ToString());
                        else
                            deals = new List<DealInfo>();
                        xtrade.SaveDeals(deals);
                    }
                    break;
                case EnumSignals.SIGNAL_CHECK_BALANCE:
                    {
                        if (signal.Data == null)
                            break;
                        JArray jarray = (JArray)signal.Data;
                        if (jarray == null || jarray.Count == 0)
                            break;
                        decimal balance = jarray.First.Value<decimal?>("Balance") ?? 0;
                        decimal equity = jarray.First.Value<decimal?>("Equity") ?? 0;
                        int Account = jarray.First.Value<int?>("Account") ?? 0;
                        xtrade.UpdateBalance(Account, balance, equity);
                    }
                    break;
                case EnumSignals.SIGNAL_UPDATE_RATES:
                    {
                        try
                        {
                            List<RatesInfo> rates = null;
                            if (signal.Data != null)
                                rates = JsonConvert.DeserializeObject<List<RatesInfo>>(signal.Data.ToString());
                            if (rates != null)
                            {
                                var usdbynrateTask = GetBYNRates();
                                double usdbynrate = usdbynrateTask.Result;
                                if (usdbynrate > 0)
                                {
                                    RatesInfo rate = new RatesInfo();
                                    rate.Ask = usdbynrate;
                                    rate.Bid = usdbynrate;
                                    rate.Symbol = "USDBYN";
                                    rates.Add(rate);
                                }
                            }
                            xtrade.UpdateRates(rates);
                        }
                        catch (Exception e)
                        {
                            log.Info(String.Format($"GetBYNUSDRates Error: {0}", e.ToString()));
                        }
                    }
                    break;
                case EnumSignals.SIGNAL_ACTIVE_ORDERS:
                    {
                        List<PositionInfo> positions = null;
                        if (signal.Data != null)
                            positions = JsonConvert.DeserializeObject<List<PositionInfo>>(signal.Data.ToString());
                        else
                            positions = new List<PositionInfo>();
                        var terminals = MainService.thisGlobal.Container.Resolve<ITerminalEvents>();
                        terminals.UpdatePositions(signal.ObjectId, signal.Value, positions);
                    }
                    break;
                /*
                case EnumSignals.SIGNAL_UPDATE_SLTP:
                {
                    List<PositionInfo> positions = null;
                    if (signal.Data != null)
                        positions = JsonConvert.DeserializeObject<List<PositionInfo>>(signal.Data.ToString());
                    else
                        positions = new List<PositionInfo>();
                    terminals.UpdateSLTP(signal.ObjectId, signal.Value, positions);
                }
                 break;
                 */
                case EnumSignals.SIGNAL_WARN_NEWS:
                    break;
                case EnumSignals.SIGNAL_DEINIT_EXPERT:
                    {
                        ExpertInfo expert = JsonConvert.DeserializeObject<ExpertInfo>(signal.Data.ToString());
                        xtrade.DeInitExpert(expert);
                    }
                    break;
                case EnumSignals.SIGNAL_DEINIT_TERMINAL:
                    {
                        ExpertInfo expert = JsonConvert.DeserializeObject<ExpertInfo>(signal.Data.ToString());
                        xtrade.DeInitTerminal(expert);
                    }
                    break;
                case EnumSignals.SIGNAL_LEVELS4SYMBOL:
                    {
                        string symbol = signal.Sym;
                        string levelsString = xtrade.Levels4Symbol(symbol);
                        var result = xtrade.CreateSignal(SignalFlags.Expert, signal.ObjectId, (EnumSignals)signal.Id, signal.ChartId);
                        result.Sym = symbol;
                        result.Data = levelsString;
                        var send = JsonConvert.SerializeObject(result);
                        if (server != null)
                            server.MulticastText(send);
                    }
                    break;
                default:
                    if (server != null)
                    {
                        var result = xtrade.SendSignal(signal);
                        if (result != null)
                        {
                            var send = JsonConvert.SerializeObject(result);
                            server.MulticastText(send);
                        }
                    }
                    break;
            }

        }

        public void ProcessMessage(WsMessage wsMessage, IMessagingServer server)
        {
            switch (wsMessage.Type)
            {
                case WsMessageType.GetAllText:
                    {
                        wsMessage.Message = log.GetAllText();
                        wsMessage.Type = WsMessageType.GetAllText;
                        var send = JsonConvert.SerializeObject(wsMessage);
                        server.MulticastText(send);
                    }
                    break;
                case WsMessageType.ClearLog:
                    {
                        log.ClearLog();
                    }
                    break;
                case WsMessageType.WriteLog:
                    {
                        log.Info(wsMessage.Message);
                    }
                    break;
                case WsMessageType.GetAllPositions:
                    {
                        WsMessage message = new WsMessage();
                        message.From = wsMessage.From;
                        message.Type = WsMessageType.GetAllPositions;
                        var terminals = MainService.thisGlobal.Container.Resolve<ITerminalEvents>();
                        message.Message = JsonConvert.SerializeObject(terminals.GetAllPositions());
                        var send = JsonConvert.SerializeObject(message);
                        server.MulticastText(send);
                    }
                    break;
                case WsMessageType.GetAllPerformance:
                    {
                        WsMessage message = new WsMessage();
                        message.From = wsMessage.From;
                        message.Type = WsMessageType.GetAllPerformance;
                        message.Message = "[]";
                        var ds = MainService.thisGlobal.Container.Resolve<DataService>();
                        int month = int.Parse(wsMessage.Message);
                        ds.StartPerf(month);
                        var send = JsonConvert.SerializeObject(message);
                        server.MulticastText(send);
                    }
                    break;
                case WsMessageType.GetAllCapital:
                    {
                        WsMessage message = new WsMessage();
                        message.From = wsMessage.From;
                        message.Type = WsMessageType.GetAllCapital;
                        message.Message = "[]";
                        var result = JsonConvert.DeserializeObject<Dictionary<string, string>>(wsMessage.Message);
                        int wallet = int.Parse(result["WalletId"]);
                        DateTime dateTimeFrom = DateTime.Parse(result["from"]);
                        DateTime dateTimeTo = DateTime.Parse(result["to"]);
                        xtrade.GetWalletBalanceRangeAsync(wallet, dateTimeFrom, dateTimeTo);
                        var send = JsonConvert.SerializeObject(message);
                        server.MulticastText(send);
                    }
                    break;
                case WsMessageType.UpdatePosition:
                    {
                        var result = JsonConvert.DeserializeObject<PositionInfo>(wsMessage.Message);
                        if (result != null)
                        {
                            var terminals = MainService.thisGlobal.Container.Resolve<ITerminalEvents>();
                            terminals.UpdatePositionFromClient(result);
                        }
                    }
                    break;
                case WsMessageType.GetLevels:
                    {
                        wsMessage.From = "Server";
                        wsMessage.Message = xtrade.Levels4Symbol(wsMessage.Message);
                        wsMessage.Type = WsMessageType.GetLevels;
                        var send = JsonConvert.SerializeObject(wsMessage);
                        server.MulticastText(send);
                    }
                    break;


                default:
                    {
                        log.Info("Undefined Message");
                        // Multicast message to all connected sessions
                        // ((WsServer)Server).MulticastText(message);
                    }
                    break;
            }
        }

        public async Task<double> GetBYNRates()
        {
            const string url = "https://www.nbrb.by/api/exrates/rates?periodicity=0";
            var client = new HttpClient();
            var stringTask = client.GetStringAsync(url);
            // double result = 0;
            await stringTask;
            if (stringTask.Result != null)
            {
                string stringData = stringTask.Result;
                List<Dictionary<string, object>> data = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(stringData);
                if (data != null)
                {
                    foreach (var item in data)
                    {
                        if (item.ContainsKey("Cur_ID"))
                        {
                            Int64 value = (Int64)item["Cur_ID"];
                            if (value == 145)
                            {
                                return (double)item["Cur_OfficialRate"];
                            }
                        }

                    }

                }
            }
            return 0;
        }


    }
}