//+------------------------------------------------------------------+
//|                                                 ThriftClient.mqh |
//|                                                 Sergei Zhuravlev |
//|                                   http://github.com/sergiovision |
//+------------------------------------------------------------------+
#property copyright "Sergei Zhuravlev"
#property link      "http://github.com/sergiovision"
#property strict

#include <XTrade\IUtils.mqh>
#include <XTrade\ITradeService.mqh>
#include <XTrade\Jason.mqh>
#include <XTrade\CommandsController.mqh>
#include <XTrade\Deal.mqh>

input string BASEURL = "http://127.0.0.1:2020";

//+------------------------------------------------------------------+
//|                                                                  |
//+------------------------------------------------------------------+
class TradeConnector : public ITradeService
{
private: 
   string  SendMethod(string action, Signal* obj);
   void    PostMethod(string action, Signal* obj);
protected:
   // Events temp vars;
   datetime storeEventTime;
   string storeParamstrEvent;   
   string storeParamstr;
   
   string oldSentstr;
   datetime prevSenttime;
   
   string headers;
   int timeout;
   int paramsBufSize;
   string baseURL;
   ExpertParams* expertParams;
public:
   TradeConnector(short Port, string EA, int DefMagic = DEFAULT_MAGIC_NUMBER)
      :ITradeService(Port, EA)
   {
      sep = StringGetCharacter(constant.PARAMS_SEPARATOR, 0);
      sepList = StringGetCharacter(constant.LIST_SEPARATOR, 0);
      magic = DefMagic;
      baseURL = BASEURL + "/api/mt";
      headers = "\r\nContent-Type: application/json\r\nAccept: application/json\r\n";
      timeout = HTTP_FINCORE_TIMEOUT;
      paramsBufSize = 4096;
      expertParams = NULL;
   }
   virtual bool Init(bool isEA);
   virtual bool CheckActive();
   virtual void Log(string message);
   virtual void SaveAllSettings(string strExpertData, string strDataOrders);
   virtual uint DeInit(int Reason);
   virtual Signal* ListenSignal(long flags, long ObjectId);
   void    ProcessSignals();
   virtual void PostSignal(Signal*   s);
   virtual void PostSignalLocally(Signal* signal);
   virtual Signal* SendSignal(Signal* s);
   virtual bool   LoadExpertParams();
   virtual void   CallLoadParams(CJAVal* pars);
   virtual string CallStoreParamsFunc();
   virtual ~TradeConnector();
   virtual void    DealsHistory(int days); 
   virtual void UpdateRates(string symbols);
   virtual string Levels4Symbol(string sym);
};

void TradeConnector::~TradeConnector()
{
}


void TradeConnector::CallLoadParams(CJAVal* pars) {
   if (pars != NULL)
      eset.obj.Deserialize(pars.ToStr());
   eset.Save(false);
}

string TradeConnector::CallStoreParamsFunc() {
    return eset.Save(true);
}

string TradeConnector::SendMethod(string action, Signal* obj) {
    string uri = StringFormat("%s/%s", baseURL, action);
    uchar data[];
    ArrayResize(data, paramsBufSize);
    ArrayInitialize(data, 0);
    StringToCharArray(obj.Serialize(),data);
    uchar resdata[];
    string resultHeaders;
    int res = WebRequest("GET", uri, headers, timeout, data, resdata, resultHeaders);
    if (res == -1) 
    { 
        isActive = false;
        Utils.Info(StringFormat("Error in WebRequest SendMethod(%s). Error code=%d. Response: %s", EnumToString(obj.type), GetLastError(), resultHeaders)); 
    } else {
       isActive = true;
       int size = ArraySize(resdata);
       //Utils.Info(StringFormat("Array Size =%d", size)); 
       //if (res < 200 || res > 299) {
           //Print("SEND response: " + res + ", (" + obj.Sym + "), " + EnumToString(obj.type));
       //}      
       if (size <= 0)
          return "";
       return CharArrayToString(resdata);
    }
    return "";
}

void TradeConnector::PostMethod(string action, Signal* obj) {
    string uri = StringFormat("%s/%s", baseURL, action);
    uchar data[];
    ArrayResize(data, paramsBufSize);
    ArrayInitialize(data, 0);
    StringToCharArray(obj.Serialize(),data);
    uchar resdata[];
    string resultHeaders;
    int res = WebRequest("POST", uri, headers, HTTP_FINCORE_OP, data, resdata, resultHeaders);
    if(res == -1)
    {
        isActive = false;
        Utils.Info(StringFormat("Error in WebRequest PostMethod(%s). Error code=%d. Response: %s", EnumToString(obj.type), GetLastError(), resultHeaders)); 
    } else 
        isActive = true;
        
    //if (res < 200 || res > 299) {
    //      Print("POST response: " + res + ", (" + obj.Sym + "), " + EnumToString(obj.type));
    //}
}

bool TradeConnector::Init(bool isEA) 
{
#ifdef   DEFINE_DLL_TCP
   Utils.SetupTcpConnection("127.0.0.1", 2022, isEA);
#endif 

   IsEA = isEA;
   int accountNumber = (int)Utils.GetAccountNumer();
   string periodStr = EnumToString((ENUM_TIMEFRAMES)Period());
   sym = Symbol();
	if (isEA)
	{
      expertParams = new ExpertParams();
      expertParams.Fill(Utils.GetAccountNumer(), Period(), sym, this.EAName, magic, 0, this.isMaster);
      Signal initsignal(SignalToServer, SIGNAL_INIT_EXPERT, 0, Utils.chartId);
      initsignal.Sym = sym;
      initsignal.SetJsonObject("Data", expertParams);
      Signal* resultSignal = SendSignal(&initsignal);
      if (resultSignal == NULL)
      {
         Utils.Info(StringFormat("InitExpert(%d, %s, %s) FAILED! Empty result returned! Reason: %s", accountNumber, periodStr, sym, expertParams.Reason));
         DELETE_PTR(resultSignal);
         DELETE_PTR(expertParams);
         return false;
      }
      DELETE_PTR(expertParams);
      expertParams = new ExpertParams(resultSignal.ValueString("Data")); 
      DELETE_PTR(resultSignal);
      if (expertParams.Magic <= 0)
      {
         Utils.Info(StringFormat("InitExpert(%d, %s, %s) FAILED! Wrong Magic Number! Reason: %s", accountNumber, periodStr, sym, expertParams.Reason));
         DELETE_PTR(expertParams);
         DELETE_PTR(resultSignal);
         return false;
      }
      magic = expertParams.Magic;
      
      isMaster = expertParams.IsMaster;
         
      string datastr = expertParams.obj["Data"].ToStr();
      bool shoudSave = StringLen(datastr) == 0;
      CallLoadParams(expertParams.obj["Data"]);
      if (shoudSave)
      {
         string savestr = CallStoreParamsFunc();
         SaveAllSettings(savestr, ""); 
      }
      isActive = true;
   } else 
   {   
      expertParams = new ExpertParams();
      this.magic = Utils.GetAccountNumer();
      expertParams.Fill(Utils.GetAccountNumer(), Period(), sym, this.EAName, magic, 0, this.isMaster);
      Signal initsignal(SignalToServer, SIGNAL_INIT_TERMINAL, Utils.GetAccountNumer(), Utils.chartId);
      initsignal.Sym = sym;
      initsignal.SetJsonObject("Data", expertParams);
      Signal* resultSignal = SendSignal(&initsignal);
      if (resultSignal == NULL)
      {
         Utils.Info(StringFormat("InitTerminal(%d) FAILED! Empty result returned! Reason: %s", accountNumber, expertParams.Reason));
         return false;
      }
      DELETE_PTR(expertParams);
      expertParams = new ExpertParams(resultSignal.ValueString("Data"));
      DELETE_PTR(resultSignal);
   }
   storeEventTime = TimeCurrent();
   prevSenttime = storeEventTime;
   return isActive;
}

void TradeConnector::ProcessSignals()
{
   Signal* signal = ListenSignal(SignalToExpert, this.magic);
   if ( signal != NULL ) {
      PostSignalLocally(signal);
   }
}

bool TradeConnector::CheckActive() {
   return isActive;
}
   
bool TradeConnector::LoadExpertParams() {
   //isActive = Connector::IsServerActive(clientString) > 0;
   return false;
}
   
void TradeConnector::SaveAllSettings(string strExpertData, string strDataOrders)
{      
   if (!IsEA)
      return;
   expertParams.Fill(Utils.GetAccountNumer(), Period(), Symbol(), this.EAName, magic, 0, this.isMaster);
   if (StringLen(strExpertData) > 0)
      expertParams.obj["Data"] = strExpertData;
   
   if (StringLen(strDataOrders) > 0)
      expertParams.obj["Orders"] = strDataOrders;

   Signal* signal = new Signal(SignalToServer, SIGNAL_SAVE_EXPERT, magic, Utils.chartId);
   signal.Sym = expertParams.Symbol;
   signal.SetJsonObject("Data", expertParams);
   PostSignal(signal);
}

uint TradeConnector::DeInit(int Reason)
{
   if (magic != FAKE_MAGICNUMBER)
   {  
      expertParams.Fill(Utils.GetAccountNumer(), Period(), Symbol(), this.EAName, magic, 0, this.isMaster);
      SignalType type = SIGNAL_DEINIT_EXPERT;
      if (!IsEA)
         type = SIGNAL_DEINIT_TERMINAL;
      Signal* signal = new Signal(SignalToServer, type, magic, Utils.chartId);
      signal.Sym = expertParams.Symbol;
      signal.SetJsonObject("Data", expertParams);
      PostSignal(signal);
   }
   DELETE_PTR(expertParams)
   return 0;
}

void TradeConnector::Log(string message)
{
   //CJAVal parameters;
   //parameters["Magic"] = magic;
   //parameters["Account"] = expertParams.Account;
   //parameters["message"] = message;
   Signal* signal = new Signal(SignalToServer, SIGNAL_POST_LOG, MagicNumber(), Utils.chartId);
   signal.Sym = Utils.Symbol;
   signal.obj["Data"] = message; // parameters.Serialize();
   PostSignal(signal);
}     

Signal* TradeConnector::ListenSignal(long flags, long ObjectId)
{
   Signal signal((SignalFlags)flags, (SignalType)0, ObjectId, Utils.chartId);
   string value = "";

   if (Utils.isHttp)
     value = SendMethod("ListenSignal", &signal);
   else 
     value = DoListenMessage(flags);
   
   if ((StringLen(value) > 0) && (value != NULL))
   {
       return new Signal(value);
   }
   return NULL;
}

Signal* TradeConnector::SendSignal(Signal* s)
{
   if ( !s.isValid() ) {
      Print("SendSignal: Wrong Signal: " + s.toString());
      return s;
   }
   string value = "";
   if (Utils.isHttp) 
   {
      value = SendMethod("SendSignal", s);
   } else 
   {
      string instr = s.Serialize();
      value = DoSendMessage(instr);
   }
   if ((StringLen(value) > 0) && (value != NULL))
   {
       return new Signal(value);
   }
   return NULL;
}

void TradeConnector::PostSignal(Signal* s)
{
   if ( !s.isValid() ) {
      Print("PostSignal: Wrong Signal: " + s.toString());
      return;
   }
   if (s.flags == SignalToExpert)
   {
       // Handle signal locally.
       PostSignalLocally(s);
       return;
   }   
   if (Utils.isHttp)
      PostMethod("PostSignal", s);
   else {
       string instr = s.Serialize();
       DoPostMessage(instr);
   }
   
   DELETE_PTR(s);
}

void TradeConnector::PostSignalLocally(Signal* signal)
{
   if (IsEA)
   {
      ushort event_id = (ushort)signal.type;
      if (event_id != 0)
      {
         this.controller.HandleSignal(event_id,signal.ObjectId,signal.Value,signal.Serialize());
      }
      DELETE_PTR(signal);

      //string ss = signal.Serialize();
      //EventChartCustom(Utils.Trade().ChartId(), event_id, signal.ObjectId, signal.Value, ss);
      //DELETE_PTR(signal);
   }
}

void TradeConnector::DealsHistory(int days) 
{
    datetime dto = TimeCurrent();
    MqlDateTime mqlDt;
    TimeToStruct(dto, mqlDt);
    mqlDt.day_of_year = mqlDt.day_of_year - days;
    mqlDt.hour = 1;
    mqlDt.min = 1;
    mqlDt.sec = 1;
    mqlDt.day = mqlDt.day - days;
    datetime from = StructToTime(mqlDt);
    if (days <= 0)
      from = 0;
    if (!HistorySelect(from, dto))
    {
         Utils.Info(StringFormat("Failed to retrieve Deals history for %d days", days));
         return;
    }
    uint total = HistoryDealsTotal(); 
    if ( total <= 0 )
       return;
    ulong ticket = 0;
    Signal* retSignal = new Signal(SignalToServer, SIGNAL_DEALS_HISTORY, MagicNumber(), Utils.chartId);
    retSignal.Sym = Utils.Symbol;
    double dailyProfit = 0;
    for ( uint i = 0;i<total;i++ )
    { 
         if ((ticket = HistoryDealGetTicket(i)) > 0) 
         {
            Deal* deal = new Deal(ticket);
            if (deal.entry == DEAL_ENTRY_IN) {
               DELETE_PTR(deal)
               continue;
            }
            if ( !this.IsEA )
               retSignal.obj["Data"].Add(deal.Persistent());
            dailyProfit += deal.profit;
            Utils.SetDailyProfit(dailyProfit);
            DELETE_PTR(deal)
        }
    }    
 
    if ( this.IsEA )
       return;  // exit if not in service

    // Run this code only in service mode
    PostSignal(retSignal);
}

string TradeConnector::Levels4Symbol(string symbolx) 
{
   string result = "";
   Signal inSignal(SignalToServer, SIGNAL_LEVELS4SYMBOL, MagicNumber(), Utils.chartId);
   inSignal.Sym = symbolx;
   inSignal.SetValue("Data", symbolx);
   Signal* resultSignal = SendSignal(&inSignal);
   if (resultSignal == NULL)
   {
      return result;
   }
   result = resultSignal.ValueString("Data"); //resultSignal.obj["Data"].ToStr();      
   DELETE_PTR(resultSignal);
   return result;
}
   
void TradeConnector::UpdateRates(string symbols)
{
   int inputlen = StringLen(symbols);
   if ( inputlen <= 0 )
      return;
   ushort u_sep = StringGetCharacter(",", 0);
   string result[];
   string inputsymbols = symbols;
   if (u_sep == StringGetCharacter(symbols, inputlen-1))
      inputsymbols = StringSubstr(symbols, 0, inputlen-1);

   StringSplit(inputsymbols, u_sep, result);
   string response;
   int count = ArraySize(result);
   if (count <= 0)
      return;
   Signal* retSignal = new Signal(SignalToServer, SIGNAL_UPDATE_RATES, MagicNumber(), Utils.chartId);
   for (int i = 0; i < count; i++) {
      MqlTick last_tick; 
      string symbolx = result[i];
      if(SymbolInfoTick(symbolx, last_tick)) 
      {
      
         CJAVal obj;
         obj["Symbol"] = symbolx;
         obj["Ask"] = last_tick.ask;
         obj["Bid"] = last_tick.bid;
         retSignal.obj["Data"].Add(obj);
      }
   }
   PostSignal(retSignal);
}

