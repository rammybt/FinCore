﻿using Autofac;
using AutoMapper;
using BusinessLogic.BusinessObjects;
using BusinessObjects;
using Newtonsoft.Json;
using NHibernate;
using NHibernate.Type;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessLogic.Repo
{
    public class DataService : IDataService
    {
        private static IWebLog log;
        private static readonly object lockDeals = new object();
        private readonly IRepository<DBAccountstate> accstates;
        private readonly IRepository<DBCurrency> currencies;
        private IRepository<DBDeals> deals;
        private readonly ExpertsRepository experts;
        private readonly IRepository<DBJobs> jobs;
        private readonly AuthRepository persons;
        private readonly IRepository<DBSettings> settings;
        private readonly IRepository<DBSymbol> symbols;
        private readonly IRepository<DBMetasymbol> metaSymbols;
        private readonly WalletsRepository wallets;
        private readonly IRepository<DBProperties> props;
        private ConcurrentDictionary<string, Rates> crates;
        private IMapper mapper;

        public DataService(IWebLog l)
        {
            symbols = new BaseRepository<DBSymbol>();
            currencies = new BaseRepository<DBCurrency>();
            settings = new BaseRepository<DBSettings>();
            jobs = new BaseRepository<DBJobs>();
            accstates = new BaseRepository<DBAccountstate>();
            experts = new ExpertsRepository();
            persons = new AuthRepository();
            wallets = new WalletsRepository(this);
            deals = new BaseRepository<DBDeals>();
            props = new BaseRepository<DBProperties>();
            metaSymbols = new BaseRepository<DBMetasymbol>();
            log = l;
            crates = new ConcurrentDictionary<string, Rates>();
            GetRates(true);
            mapper = MainService.thisGlobal.Container.Resolve<IMapper>();
#if DEBUG
            mapper.ConfigurationProvider.AssertConfigurationIsValid();
#endif
        }

        public List<CurrencyInfo> GetCurrencies()
        {
            List<CurrencyInfo> result = new List<CurrencyInfo>();
            try
            {
                currencies.GetAll().ForEach(currency =>
                {
                    var curr = new CurrencyInfo();
                    curr.Id = (short)currency.Id;
                    curr.Name = currency.Name;
                    curr.Retired = currency.Enabled.Value > 0 ? false : true;
                    result.Add(curr);
                });
            }
            catch (Exception e)
            {
                log.Error("Error: GetCurrencies: " + e);
            }

            return result;
        }

        public string GetGlobalProp(string name)
        {
            try
            {
                var gvars = settings.GetAll().Where(x => x.Propertyname.Equals(name));
                if (gvars.Count() > 0)
                {
                    DBSettings gvar = gvars.First();
                    return gvar.Value;
                }
            }
            catch (Exception e)
            {
                log.Error("Error: GetGlobalProp: " + e);
            }

            return "";
        }

        public void SetGlobalProp(string name, string value)
        {
            var gvars = settings.GetAll().Where(x => x.Propertyname == name);
            if (gvars.Count() > 0)
            {
                DBSettings gvar = gvars.First();
                gvar.Value = value;
                settings.Update(gvar);
            }
            else
            {
                var gvar = new DBSettings();
                gvar.Propertyname = name;
                gvar.Value = value;
                settings.Insert(gvar);
            }
        }

        public decimal ConvertToUSD(decimal value, string valueCurrency)
        {
            decimal result = value;
            if (valueCurrency.Equals("USD") || (value == 0))
                return result;
            string c1sym = valueCurrency + "USD";
            if (crates.ContainsKey(c1sym))
            {
                Rates finalRate = crates[c1sym];
                result = result * finalRate.Ratebid;
            }
            else
            {
                string c2sym = "USD" + valueCurrency;
                if (crates.ContainsKey(c2sym))
                {
                    Rates finalRate = crates[c2sym];
                    result = result / finalRate.Rateask;
                }
            }
            return result;
        }

        public void UpdateRates(List<RatesInfo> rInfo)
        {
            try
            {
                using (ISession Session = ConnectionHelper.CreateNewSession())
                {
                    var dbrates = Session.Query<DBRates>();
                    foreach (var rate in dbrates)
                    {
                        string symbolName = rate.Metasymbol.Name;
                        var currentRate = rInfo.FirstOrDefault(d => d.Symbol.CompareTo(symbolName) == 0);
                        if (currentRate == null)
                            continue;
                        using (ITransaction Transaction = Session.BeginTransaction())
                        {
                            rate.Rateask = new decimal(currentRate.Ask);
                            rate.Ratebid = new decimal(currentRate.Bid);
                            rate.Lastupdate = DateTime.UtcNow;
                            Session.Update(rate);
                            Transaction.Commit();
                        }
                    }
                }
                GetRates(true);
            }
            catch (Exception e)
            {
                log.Error("Error: UpdateRates: " + e);
            }

        }

        public bool isSameDay(DateTime d1, DateTime d2)
        {
            return (d1.DayOfYear == d2.DayOfYear) && (d2.Year == d1.Year);
        }

        public List<DealInfo> TodayDeals()
        {
            List<DealInfo> result = new List<DealInfo>();
            try
            {
                DateTime now = DateTime.Now;
                using (ISession Session = ConnectionHelper.CreateNewSession())
                {
                    var deals = Session.Query<DBDeals>().OrderByDescending(x => x.Closetime);
                    foreach (var dbd in deals)
                        if (isSameDay(dbd.Closetime.Value, now))
                        {
                            result.Add(toDTO(dbd));
                        }

                }
            }
            catch (Exception e)
            {
                log.Error("Error: TodayDeals: " + e);
            }
            return result;
        }

        public void UpdateBalance(int AccountNumber, decimal Balance, decimal Equity)
        {
            using (ISession Session = ConnectionHelper.CreateNewSession())
            {
                var terms = Session.Query<DBTerminal>().Where(x => x.Accountnumber == AccountNumber);
                if (terms == null || terms.Count() <= 0)
                    return;
                DBTerminal terminal = terms.FirstOrDefault();
                if (terminal == null)
                    return;
                if (terminal.Account == null)
                    return;
                using (ITransaction Transaction = Session.BeginTransaction())
                {
                    terminal.Account.Balance = Balance;
                    terminal.Account.Equity = Equity;
                    terminal.Account.Lastupdate = DateTime.UtcNow;
                    Session.Update(terminal);
                    Transaction.Commit();
                }

                var acc = Session.Query<DBAccountstate>().Where(x => x.Account.Id == terminal.Account.Id)
                    .OrderByDescending(x => x.Date);
                using (ITransaction Transaction = Session.BeginTransaction())
                {
                    if (acc.Any())
                    {
                        DBAccountstate state = null;
                        state = acc.FirstOrDefault();
                        if (state == null || state.Date.DayOfYear != DateTime.Today.DayOfYear)
                        {
                            var newstate = new DBAccountstate();
                            if (state == null)
                                newstate.Account = terminal.Account;
                            else
                                newstate.Account = state.Account;
                            newstate.Balance = Balance;
                            newstate.Comment = "Autoupdate";
                            newstate.Date = DateTime.UtcNow;
                            Session.Save(newstate);
                        }
                        else
                        {
                            state.Balance = Balance;
                            state.Comment = "Autoupdate";
                            state.Date = DateTime.UtcNow;
                            Session.Update(state);
                        }

                        Transaction.Commit();
                    }
                }
            }
        }

        /*
        public void MigrateAdvisers()
        {
            try
            {
                experts.MigrateAdvisersData();
            }
            catch (Exception e)
            {
                log.Error("Error: MigrateAdvisers: " + e);
            }

        }*/


        public List<Wallet> GetWalletsState(DateTime date)
        {
            List<Wallet> result = new List<Wallet>();
            try
            {
                return wallets.GetWalletsState(date);
            }
            catch (Exception e)
            {
                log.Error("Error: GetCurrentWalletsState: " + e);
            }

            return result;
        }

        public List<Asset> AssetsDistribution(int type)
        {
            return wallets.AssetsDistribution(type);
        }

        public void SaveDeals(List<DealInfo> deals)
        {
            if (deals == null)
                return;
            if (deals.Count() <= 0)
                return;
            try
            {
                lock (lockDeals)
                {
                    int i = 0;
                    using (ISession Session = ConnectionHelper.CreateNewSession())
                    {
                        foreach (var deal in deals.OrderBy(x => x.CloseTime))
                        {
                            var sym = getSymbolByName(deal.Symbol);
                            if (sym == null)
                                continue;
                            DBDeals dbDeal = Session.Get<DBDeals>((int)deal.Ticket);
                            if (dbDeal == null)
                            {
                                if (getDealById(Session, deal.Ticket) != null)
                                    continue;
                                try
                                {
                                    using (ITransaction Transaction = Session.BeginTransaction())
                                    {
                                        dbDeal = new DBDeals();
                                        dbDeal.Dealid = (int)deal.Ticket;
                                        dbDeal.Symbol = getSymbolByName(deal.Symbol);
                                        dbDeal.Terminal = getBDTerminalByNumber(Session, deal.Account);
                                        dbDeal.Adviser = getAdviserByMagicNumber(Session, deal.Magic);
                                        dbDeal.Id = (int)deal.Ticket;
                                        DateTime closeTime;
                                        if (DateTime.TryParse(deal.CloseTime, out closeTime))
                                            dbDeal.Closetime = DateTime.Parse(deal.CloseTime);
                                        dbDeal.Comment = deal.Comment;
                                        dbDeal.Commission = (decimal)deal.Commission;
                                        DateTime openTime;
                                        if (DateTime.TryParse(deal.OpenTime, out openTime))
                                            dbDeal.Opentime = DateTime.Parse(deal.OpenTime);
                                        dbDeal.Orderid = (int)deal.OrderId;
                                        dbDeal.Profit = (decimal)deal.Profit;
                                        dbDeal.Price = (decimal)deal.ClosePrice;
                                        dbDeal.Swap = (decimal)deal.SwapValue;
                                        dbDeal.Typ = deal.Type;
                                        dbDeal.Volume = (decimal)deal.Lots;
                                        Session.Save(dbDeal);
                                        Transaction.Commit();
                                        i++;
                                    }
                                }
                                catch (Exception)
                                {
                                    log.Log($"Deal {deal.Ticket}:{deal.Symbol} failed to be saved in database");
                                }
                            }
                        }
                    }

                    if (i > 0)
                        log.Log($"Saved {i} history deals in database");
                }
            }
            catch (Exception e)
            {
                string message = "Error: DataService.SaveDeals: " + e;
                log.Error(message);
                log.Log(message);
            }
        }

        public IEnumerable<MetaSymbolStat> MetaSymbolStatistics(int AccountType)
        {
            List<MetaSymbolStat> result = new List<MetaSymbolStat>();
            try
            {
                bool IsDemoAccount = AccountType > 0 ? true : false;
                using (ISession Session = ConnectionHelper.CreateNewSession())
                {
                    var symbols = Session.Query<DBMetasymbol>().Where(x => x.Retired == false).ToList();
                    foreach (var sym in symbols)
                    {
                        var deals = Session.Query<DBDeals>().Where(x =>
                            x.Symbol.Metasymbol.Id == sym.Id && x.Terminal.Demo == IsDemoAccount);
                        decimal sumProfit = 0;
                        int countTrades = 0;
                        foreach (var deal in deals)
                        {
                            sumProfit += ConvertToUSD(deal.Profit, deal.Terminal.Account.Currency.Name);
                            countTrades++;
                        }

                        if (countTrades <= 10)
                            continue;
                        MetaSymbolStat mss = new MetaSymbolStat();
                        mss.MetaId = sym.Id;
                        mss.Name = sym.Name;
                        mss.Description = sym.Description;
                        mss.TotalProfit = sumProfit;
                        mss.NumOfTrades = countTrades;
                        mss.ProfitPerTrade = sumProfit / countTrades;
                        mss.Date = DateTime.Now;
                        result.Add(mss);
                    }
                }
            }
            catch (Exception e)
            {
                log.Error("Error in MetaSymbolStat : " + e);
            }

            return result.OrderByDescending(x => x.ProfitPerTrade);
        }

        public void StartPerf(int month)
        {
            var task = new Task(() => PerfAsync(month));
            task.Start();
        }

        private void PerfAsync(int month)
        {
            var service = MainService.thisGlobal.Container.Resolve<IMessagingService>();
            try
            {
                using (ISession Session = ConnectionHelper.CreateNewSession())
                {
                    var rateList = Session.Query<DBRates>().Where(x => x.Retired == false).ToList();
                    DateTime now = DateTime.Now;
                    int dayFrom = 1;
                    int year = now.Year;
                    if (now.Month < month + 1) year--;

                    DateTime from = new DateTime(year, month + 1, dayFrom);
                    int dayTo = now.Month == month + 1 ? now.Day : DateTime.DaysInMonth(year, month + 1);
                    DateTime to = new DateTime(year, month + 1, dayTo);
                    var Accounts = Session.Query<DBAccount>(); // .Where(x => (x.Retired == false));
                    //var Deals = Session.Query<DBDeals>().Where(x => x.Terminal.Demo == false);
                    for (int i = dayFrom; i <= dayTo; i++)
                    {
                        DateTime forDate = new DateTime(year, month + 1, i);
                        DateTime forDateEnd = new DateTime(year, month + 1, i, 23, 50, 0);
                        TimeStat ts = new TimeStat();
                        ts.X = i;
                        ts.Date = forDate;
                        ts.Period = TimePeriod.Daily;
                        ts.Gains = 0;
                        ts.Losses = 0;
                        foreach (var acc in Accounts)
                        {
                            if (acc.Terminal != null)
                                if (acc.Terminal.Demo)
                                    continue;
                            var accStateAll = Session.Query<DBAccountstate>().Where(x => x.Account.Id == acc.Id);
                            var accResultsStart = accStateAll.Where(x => x.Date <= forDate)
                                .OrderByDescending(x => x.Date);
                            var accResultsEnd = accStateAll.Where(x => x.Date <= forDateEnd)
                                .OrderByDescending(x => x.Date);

                            if (accResultsEnd == null || accResultsEnd.Count() == 0)
                                continue;
                            if (accResultsStart == null || accResultsStart.Count() == 0)
                                continue;

                            var accStateEnd = accResultsEnd.FirstOrDefault();
                            decimal balanceStart = new decimal(0);
                            decimal balanceEnd = new decimal(0);
                            if (accStateEnd != null)
                            {
                                balanceEnd = ConvertToUSD(accStateEnd.Balance, acc.Currency.Name);
                                if (acc.Typ > 0)
                                    ts.InvestingValue += balanceEnd;

                                ts.CheckingValue += balanceEnd;
                            }

                            var accStateStart = accResultsStart.FirstOrDefault();
                            if (accStateStart != null)
                            {
                                balanceStart = ConvertToUSD(accStateStart.Balance, acc.Currency.Name);
                                if (acc.Typ > 0)
                                    ts.InvestingChange += balanceStart;

                                ts.CheckingChange += balanceStart;
                            }
                        }

                        ts.CheckingChange = ts.CheckingValue - ts.CheckingChange;
                        ts.InvestingChange = ts.InvestingValue - ts.InvestingChange;
                        if (ts.CheckingChange > 0)
                            ts.Gains = ts.CheckingChange;
                        else
                            ts.Losses = Math.Abs(ts.CheckingChange);

                        ts.CheckingChange = Math.Round(ts.CheckingChange, 2);
                        ts.InvestingChange = Math.Round(ts.InvestingChange, 2);
                        ts.CheckingValue = Math.Round(ts.CheckingValue, 2);
                        ts.InvestingValue = Math.Round(ts.InvestingValue, 2);
                        ts.Losses = Math.Round(ts.Losses, 2);
                        ts.Gains = Math.Round(ts.Gains, 2);

                        service.SendMessage(WsMessageType.ChartValue, ts);
                    }
                }
            }
            catch (Exception e)
            {
                log.Error("Error in Performance : " + e);
            }
            service.SendMessage(WsMessageType.ChartDone, "");
        }

        public List<TimeStat> Performance(int month, TimePeriod period)
        {
            List<TimeStat> result = new List<TimeStat>();
            try
            {
                using (ISession Session = ConnectionHelper.CreateNewSession())
                {
                    var rateList = Session.Query<DBRates>().Where(x => x.Retired == false).ToList();
                    DateTime now = DateTime.Now;
                    int dayFrom = 1;
                    int year = now.Year;
                    if (now.Month < month + 1) year--;

                    DateTime from = new DateTime(year, month + 1, dayFrom);
                    int dayTo = now.Month == month + 1 ? now.Day : DateTime.DaysInMonth(year, month + 1);
                    DateTime to = new DateTime(year, month + 1, dayTo);
                    var Accounts = Session.Query<DBAccount>(); // .Where(x => (x.Retired == false));
                    //var Deals = Session.Query<DBDeals>().Where(x => x.Terminal.Demo == false);
                    for (int i = dayFrom; i <= dayTo; i++)
                    {
                        DateTime forDate = new DateTime(year, month + 1, i);
                        DateTime forDateEnd = new DateTime(year, month + 1, i, 23, 50, 0);
                        TimeStat ts = new TimeStat();
                        ts.X = i;
                        ts.Date = forDate;
                        ts.Period = period;
                        ts.Gains = 0;
                        ts.Losses = 0;
                        foreach (var acc in Accounts)
                        {
                            if (acc.Terminal != null)
                                if (acc.Terminal.Demo)
                                    continue;
                            var accStateAll = Session.Query<DBAccountstate>().Where(x => x.Account.Id == acc.Id);
                            var accResultsStart = accStateAll.Where(x => x.Date <= forDate)
                                .OrderByDescending(x => x.Date);
                            var accResultsEnd = accStateAll.Where(x => x.Date <= forDateEnd)
                                .OrderByDescending(x => x.Date);

                            if (accResultsEnd == null || accResultsEnd.Count() == 0)
                                continue;
                            if (accResultsStart == null || accResultsStart.Count() == 0)
                                continue;

                            var accStateEnd = accResultsEnd.FirstOrDefault();
                            decimal balanceStart = new decimal(0);
                            decimal balanceEnd = new decimal(0);
                            if (accStateEnd != null)
                            {
                                balanceEnd = ConvertToUSD(accStateEnd.Balance, acc.Currency.Name);
                                if (acc.Typ > 0)
                                    ts.InvestingValue += balanceEnd;

                                ts.CheckingValue += balanceEnd;
                            }

                            var accStateStart = accResultsStart.FirstOrDefault();
                            if (accStateStart != null)
                            {
                                balanceStart = ConvertToUSD(accStateStart.Balance, acc.Currency.Name);
                                if (acc.Typ > 0)
                                    ts.InvestingChange += balanceStart;

                                ts.CheckingChange += balanceStart;
                            }

                            /*if (balanceStart > balanceEnd)
                            {
                                ts.Losses += (balanceStart - balanceEnd);
                            }
                            if (balanceEnd > balanceStart)
                            {
                                ts.Gains += (balanceEnd - balanceStart);
                            }*/
                        }

                        ts.CheckingChange = ts.CheckingValue - ts.CheckingChange;
                        ts.InvestingChange = ts.InvestingValue - ts.InvestingChange;
                        if (ts.CheckingChange > 0)
                            ts.Gains = ts.CheckingChange;
                        else
                            ts.Losses = Math.Abs(ts.CheckingChange);

                        ts.CheckingChange = Math.Round(ts.CheckingChange, 2);
                        ts.InvestingChange = Math.Round(ts.InvestingChange, 2);
                        ts.CheckingValue = Math.Round(ts.CheckingValue, 2);
                        ts.InvestingValue = Math.Round(ts.InvestingValue, 2);
                        ts.Losses = Math.Round(ts.Losses, 2);
                        ts.Gains = Math.Round(ts.Gains, 2);

                        result.Add(ts);
                    }
                }
            }
            catch (Exception e)
            {
                log.Error("Error in Performance : " + e);
            }

            return result;
        }

        public List<DealInfo> GetDeals()
        {
            List<DealInfo> result = new List<DealInfo>();
            try
            {
                using (ISession Session = ConnectionHelper.CreateNewSession())
                {
                    var deals = Session.Query<DBDeals>().OrderByDescending(x => x.Closetime);
                    foreach (var dbd in deals)
                        result.Add(toDTO(dbd));
                }
            }
            catch (Exception e)
            {
                log.Error("Error: GetDeals: " + e);
            }
            return result;
        }

        public ConcurrentDictionary<string, Rates> GetRates(bool IsReread)
        {
            try
            {
                if (crates.Count() > 0 && !IsReread)
                    return crates;
                using (ISession Session = ConnectionHelper.CreateNewSession())
                {
                    var dbrates = Session.Query<DBRates>();
                    foreach (var dbr in dbrates)
                    {
                        var r = toDTO(dbr);
                        crates[r.MetaSymbol] = r;
                    }
                }
            }
            catch (Exception e)
            {
                log.Error("Error: GetRates: " + e);
            }
            return crates;
        }

        public static bool toDTO(DBAccount a, ref Account result)
        {
            try
            {
                result.Id = a.Id;
                result.Number = a.Number;
                result.Balance = a.Balance;
                if (a.Terminal != null)
                    result.TerminalId = a.Terminal.Id;
                result.Lastupdate = a.Lastupdate;
                result.Description = a.Description;
                result.Equity = a.Equity;
                if (a.Person != null)
                    result.PersonId = a.Person.Id;
                if (a.Currency != null)
                    result.CurrencyStr = a.Currency.Name;
                result.Retired = a.Retired;
                if (a.Wallet != null)
                    result.WalletId = a.Wallet.Id;
                result.Typ = (AccountType)a.Typ;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool toDTO(DBTerminal t, ref Terminal result)
        {
            try
            {
                result.AccountNumber = t.Accountnumber.Value;
                result.Broker = t.Broker;
                result.CodeBase = t.Codebase;
                result.Retired = t.Retired;
                result.FullPath = t.Fullpath;
                result.Demo = t.Demo;
                result.Stopped = t.Stopped;
                result.Id = t.Id;
                if (t.Account != null)
                    result.Currency = t.Account.Currency.Name;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public DealInfo toDTO(DBDeals deal)
        {
            DealInfo result = new DealInfo();
            // result.ClosePrice = deal;
            if (deal.Closetime.HasValue)
                result.CloseTime = deal.Closetime.Value.ToString(xtradeConstants.MTDATETIMEFORMAT);
            result.Comment = deal.Comment;
            result.Commission = (double)deal.Commission;
            result.Lots = (double)deal.Volume;
            if (deal.Adviser != null) result.Magic = deal.Adviser.Id;

            result.OpenPrice = (double)deal.Price;
            result.OpenTime = deal.Opentime.ToString(xtradeConstants.MTDATETIMEFORMAT);
            result.Profit = (double)deal.Profit;
            if (deal.Terminal != null)
            {
                result.Account = deal.Terminal.Accountnumber.Value;
                result.AccountName = deal.Terminal.Broker;
            }

            result.SwapValue = (double)deal.Swap;
            if (deal.Symbol != null)
                result.Symbol = deal.Symbol.Name;
            if (deal.Orderid.HasValue)
                result.Ticket = deal.Orderid.Value;
            result.Type = (sbyte)deal.Typ;
            return result;
        }

        public Rates toDTO(DBRates rates)
        {
            Rates result = new Rates();
            result.MetaSymbol = rates.Metasymbol.Name;
            result.C1 = rates.Metasymbol.C1;
            result.C2 = rates.Metasymbol.C2;
            result.Ratebid = rates.Ratebid;
            result.Rateask = rates.Rateask;
            result.Retired = rates.Retired;
            if (rates.Lastupdate.HasValue)
                result.Lastupdate = rates.Lastupdate.Value;
            else
                result.Lastupdate = DateTime.UtcNow;
            return result;
        }

        #region LocalFuncs

        private bool toPropsDTO(DBProperties p, ref DynamicProperties result)
        {
            try
            {
                result.ID = p.ID;
                result.entityType = p.entityType;
                result.objId = p.objId;
                result.Vals = p.Vals;
                result.updated = p.updated;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public IEnumerable<DynamicProperties> GetAllProperties()
        {
            try
            {
                List<DynamicProperties> results = new List<DynamicProperties>();
                var result = props.GetAll();
                if (!result.Any())
                    return results;
                result.ForEach(x =>
                {
                    DynamicProperties dynProps = new DynamicProperties();
                    if (toPropsDTO(x, ref dynProps))
                        results.Add(dynProps);
                });
                return results;
            }
            catch (Exception e)
            {
                log.Error("Error: GetAllProperties: " + e);
            }
            return null;
        }

        public DynamicProperties GetPropertiesInstance(short entityType, int objId)
        {
            try
            {
                DynamicProperties newdP = new DynamicProperties();
                var result = props.GetAll().Where(x => (x.entityType == entityType) && (x.objId == objId));
                if (result.Any())
                    if (toPropsDTO(result.FirstOrDefault(), ref newdP))
                        return newdP;
                using (ISession Session = ConnectionHelper.CreateNewSession())
                {
                    using (ITransaction Transaction = Session.BeginTransaction())
                    {
                        var gvar = new DBProperties();
                        // gvar.ID = newdP.ID; // ID should be Autogenerated by DB
                        gvar.entityType = (short)entityType;
                        gvar.objId = objId;
                        Dictionary<string, DynamicProperty> defProps = new Dictionary<string, DynamicProperty>();
                        defProps = DefaultProperties.fillProperties(ref defProps, (EntitiesEnum)entityType, -1, objId, "");
                        gvar.Vals = JsonConvert.SerializeObject(defProps);
                        gvar.updated = DateTime.UtcNow;
                        Session.Save(gvar);
                        Transaction.Commit();
                        if (toPropsDTO(gvar, ref newdP))
                            return newdP;
                    }
                }
            }
            catch (Exception e)
            {
                log.Error("Error: GetPropertiesInstance: " + e);
            }
            return null;
        }

        public bool SavePropertiesInstance(DynamicProperties newdP)
        {
            try
            {
                using (ISession Session = ConnectionHelper.CreateNewSession())
                {
                    using (ITransaction Transaction = Session.BeginTransaction())
                    {
                        var result = Session.Get<DBProperties>(newdP.ID);
                        if (result != null)
                        {
                            result.entityType = newdP.entityType;
                            result.objId = newdP.objId;
                            result.Vals = newdP.Vals;
                            result.updated = DateTime.UtcNow;
                            Session.Update(result);
                        }
                        else
                        {
                            var gvar = new DBProperties();
                            // gvar.ID = newdP.ID; // ID should be Autogenerated by DB
                            gvar.entityType = (short)newdP.entityType;
                            gvar.objId = newdP.objId;
                            gvar.Vals = newdP.Vals;
                            gvar.updated = DateTime.UtcNow;
                            Session.Save(gvar);
                        }
                        Transaction.Commit();
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                log.Error("Error: UpdateProperties: " + e);
            }
            return false;
        }

        public IEnumerable<DBJobs> GetDBActiveJobsList()
        {
            try
            {
                var result = jobs.GetAll().Where(x => x.Disabled == false);
                return result;
            }
            catch (Exception e)
            {
                log.Error("Error: GetDBActiveJobsList: " + e);
            }

            return null;
        }

        public DBAdviser getAdviserByMagicNumber(ISession Session, long magicNumber)
        {
            try
            {
                DBAdviser adviser = Session.Get<DBAdviser>((int)magicNumber);
                return adviser;
            }
            catch (Exception e)
            {
                log.Error("Error: getAdviserByMagicNumber: " + e);
            }

            return null;
        }

        public DBCurrency getCurrencyID(string currencyStr)
        {
            try
            {
                var result = currencies.GetAll().Where(x => x.Name.Equals(currencyStr));
                if (result.Any())
                    return result.First();
            }
            catch (Exception e)
            {
                log.Error("Error: getAdviserByMagicNumber: " + e);
            }

            return null;
        }

        public Terminal getTerminalByNumber(ISession Session, long AccountNumber)
        {
            try
            {
                var result = Session.Query<DBTerminal>().Where(x => x.Accountnumber == (int)AccountNumber);
                if (result.Any())
                {
                    var term = result.FirstOrDefault();
                    Terminal terminal = new Terminal();
                    if ((term != null) && toDTO(term, ref terminal))
                        return terminal;
                }
            }
            catch (Exception e)
            {
                log.Error("Error: getTerminalByNumber: " + e);
            }
            return null;
        }

        public DBTerminal getBDTerminalByNumber(ISession Session, long AccountNumber)
        {
            try
            {
                var result = Session.Query<DBTerminal>().Where(x => x.Accountnumber == (int)AccountNumber);
                if (result.Any()) return result.FirstOrDefault();
            }
            catch (Exception e)
            {
                log.Error("Error: getTerminalByNumber: " + e);
            }

            return null;
        }

        public DBDeals getDealById(ISession Session, long DealId)
        {
            try
            {
                var result = Session.Query<DBDeals>().Where(x => x.Dealid == (int)DealId);
                if (result.Any()) return result.FirstOrDefault();
            }
            catch (Exception e)
            {
                log.Error("Error: getDealById: " + e);
            }

            return null;
        }



        public DBSymbol getSymbolByName(string SymbolStr)
        {
            try
            {
                var result = symbols.GetAll().Where(x => x.Name.Equals(SymbolStr));
                if (result.Any())
                    return result.First();
            }
            catch (Exception e)
            {
                log.Error("Error: getSymbolByName: " + e);
            }

            return null;
        }

        public DBAdviser getAdviser(ISession Session, int term_id, int sym_id, string ea)
        {
            try
            {
                var result = Session.Query<DBAdviser>().Where(x =>
                    x.Terminal.Id == term_id && x.Symbol.Id == sym_id && x.Name == ea && x.Retired == false);
                if (result != null && result.Count() > 0)
                    return result.FirstOrDefault();
            }
            catch (Exception e)
            {
                log.Error("Error: getSymbolByName: " + e);
            }
            return null;
        }

        public void SaveInsertAdviser(ISession Session, DBAdviser toAdd)
        {
            using (ITransaction Transaction = Session.BeginTransaction())
            {
                if (toAdd.Id == 0)
                    Session.Save(toAdd);
                else
                    Session.Update(toAdd);

                Transaction.Commit();
            }
        }


        public void SaveInsertWaletState(DBAccountstate toAdd)
        {
            try
            {
                var accState = accstates.Insert(toAdd);
                if (accState == null)
                    return;
                using (ISession Session = ConnectionHelper.CreateNewSession())
                {
                    using (ITransaction Transaction = Session.BeginTransaction())
                    {
                        if (accState.Account != null)
                        {
                            var account = Session.Get<DBAccount>(accState.Account.Id);
                            if (account != null)
                            {
                                account.Balance = accState.Balance;
                                account.Lastupdate = DateTime.UtcNow;
                                Session.Update(account);
                            }
                        }

                        Transaction.Commit();
                    }
                }
            }
            catch (Exception e)
            {
                log.Error("Error: SaveInsertWaletState: " + e);
            }
        }

        public Person LoginPerson(string mail, string password)
        {
            var result = persons.FindUser(mail, password);
            return result;
        }

        public IList<T> ExecuteNativeQuery<T>(ISession session, string queryString, string entityParamName,
            Tuple<string, object, IType>[] parameters = null)
        {
            ISQLQuery query = session.CreateSQLQuery(queryString).AddEntity(entityParamName, typeof(T));
            if (parameters != null)
                for (int i = 0; i < parameters.Length; i++)
                    query.SetParameter(parameters[i].Item1, parameters[i].Item2, parameters[i].Item3);

            return query.List<T>();
        }

        public object GetObjects(EntitiesEnum type)
        {
            var t = MainService.thisGlobal.EnumToType(type);
            if (t == null)
                return "";
            List<object> result = new List<object>();
            using (ISession Session = ConnectionHelper.CreateNewSession())
            {
                Session.Query<object>(t.Item1.FullName)
                    .ToList()
                    .ForEach(item =>
                    {
                        var dtObj = mapper.Map(item, t.Item1, t.Item2);
                        result.Add(dtObj);
                    });
                return result;
            }
        }

        public object GetChildObjects(EntitiesEnum parentType, EntitiesEnum childType, int parentKey)
        {
            var parentTuple = MainService.thisGlobal.EnumToType(parentType);
            if (parentTuple == null)
                return "";
            var childTuple = MainService.thisGlobal.EnumToType(childType);
            if (childTuple == null)
                return "";

            List<object> result = new List<object>();
            using (ISession Session = ConnectionHelper.CreateNewSession())
            {
                if ((parentType == EntitiesEnum.MetaSymbol) && (childType == EntitiesEnum.Symbol))
                {
                    var list = Session.QueryOver<DBSymbol>()
                        .JoinQueryOver<DBMetasymbol>(master => master.Metasymbol)
                        .Where(parent => parent.Id == parentKey)
                        .List();
                    if (list.Any())
                    {
                        foreach (var item in list)
                        {
                            var dtObj = mapper.Map(item, childTuple.Item1, childTuple.Item2);
                            result.Add(dtObj);
                        }
                        return result;

                    }
                }
                if ((parentType == EntitiesEnum.MetaSymbol) && (childType == EntitiesEnum.Adviser))
                {
                    var list = Session.QueryOver<DBAdviser>()
                        .JoinQueryOver<DBSymbol>(master => master.Symbol)
                        .JoinQueryOver<DBMetasymbol>(master => master.Metasymbol)
                        .Where(parent => parent.Id == parentKey)
                        .List();
                    if (list.Any())
                    {
                        foreach (var item in list)
                        {
                            var dtObj = mapper.Map(item, childTuple.Item1, childTuple.Item2);
                            result.Add(dtObj);
                        }
                        return result;
                    }
                }

                if ((parentType == EntitiesEnum.Wallet) && (childType == EntitiesEnum.Account))
                {
                    var list = Session.QueryOver<DBAccount>()
                        .JoinQueryOver<DBWallet>(master => master.Wallet)
                        .Where(parent => parent.Id == parentKey)
                        .List();
                    if (list.Any())
                    {
                        foreach (var item in list)
                        {
                            var dtObj = mapper.Map(item, childTuple.Item1, childTuple.Item2);
                            result.Add(dtObj);
                        }
                        return result;
                    }
                }

                if ((parentType == EntitiesEnum.Wallet) && (childType == EntitiesEnum.Terminal))
                {
                    var list = Session.QueryOver<DBTerminal>()
                        .JoinQueryOver<DBAccount>(master => master.Account)
                        .JoinQueryOver<DBWallet>(master => master.Wallet)
                        .Where(parent => parent.Id == parentKey)
                        .List();
                    if (list.Any())
                    {
                        foreach (var item in list)
                        {
                            var dtObj = mapper.Map(item, childTuple.Item1, childTuple.Item2);
                            result.Add(dtObj);
                        }
                        return result;
                    }
                }
                return result;
            }
        }

        public object GetObject(EntitiesEnum type, int id)
        {
            var t = MainService.thisGlobal.EnumToType(type);
            if (t == null)
                return "";
            List<object> result = new List<object>();
            using (ISession Session = ConnectionHelper.CreateNewSession())
            {
                object dbObj = Session.Get(t.Item1, id);
                if (dbObj == null)
                    return "";
                var obj = mapper.Map(dbObj, t.Item1, t.Item2);
                return obj;
            }
        }

        public int InsertObject(EntitiesEnum type, string values)
        {
            var tuple = MainService.thisGlobal.EnumToType(type);
            if (tuple == null)
                return -1;
            using (ISession Session = ConnectionHelper.CreateNewSession())
            {
                object obj = JsonConvert.DeserializeObject(values, tuple.Item2);
                if (obj == null)
                    return -1;
                using (ITransaction Transaction = Session.BeginTransaction())
                {
                    var dbNew = mapper.Map(obj, tuple.Item2, tuple.Item1);
                    object id = Session.Save(dbNew);
                    Transaction.Commit();
                    return (int)id;
                }
            }
        }

        public int UpdateObject(EntitiesEnum type, int id, string values)
        {
            using (ISession Session = ConnectionHelper.CreateNewSession())
            {
                var tuple = MainService.thisGlobal.EnumToType(type);
                if (tuple == null)
                    return -1;
                object obj = JsonConvert.DeserializeObject(values, tuple.Item2);
                if (obj == null)
                    return -1;
                object dbObj = Session.Get(tuple.Item1, id);
                if (dbObj == null)
                    return -1;
                using (ITransaction Transaction = Session.BeginTransaction())
                {
                    var dbNew = mapper.Map(obj, dbObj, tuple.Item2, tuple.Item1);
                    Session.Update(dbNew);
                    Transaction.Commit();
                    return 1;
                }
            }
        }

        public int DeleteObject(EntitiesEnum type, int id)
        {
            var tuple = MainService.thisGlobal.EnumToType(type);
            if (tuple == null)
                return -1;
            using (ISession Session = ConnectionHelper.CreateNewSession())
            {
                using (ITransaction Transaction = Session.BeginTransaction())
                {
                    Session.Delete(Session.Load(tuple.Item1, id));
                    Transaction.Commit();
                }
            }
            return 1;
        }

        #endregion
    }
}