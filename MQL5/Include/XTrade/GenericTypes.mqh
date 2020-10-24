//+------------------------------------------------------------------+
//|                                                 ThriftClient.mqh |
//|                                                 Sergei Zhuravlev |
//|                                   http://github.com/sergiovision |
//+------------------------------------------------------------------+
#property copyright "Sergei Zhuravlev"
#property link      "http://github.com/sergiovision"
#property strict


#include <XTrade\Jason.mqh>

#ifdef DEFINE_DLL_TCP

#import "MTLoader.dll"
      string SetupLib(string dllName, string mainClass, string parameters);
#import 

#import "MQLConnector.dll"
      string GetLevels(string symbol, long chartId);
      string DestroyLib(long chartId);
      string DoListenMessage(long lScope);
      string DoSendMessage(string message);
      void DoPostMessage(string message);
#import 

#endif 


enum ENUM_TRAILING  
{
    TrailingDefault,
    TrailingManual,
    TrailingStairs,
    TrailingFilter,
    TrailingSignal,
    TrailingByFractals,
    TrailingByShadows,
    TrailingByATR,
    TrailingUdavka,
    TrailingByTime,
    TrailingByPriceChannel,
    TrailingFiftyFifty,
    TrailEachNewBar
};

#define TRAILS_COUNT  13

enum ENUM_ORDERROLE  
{
    RegularTrail, 
    GridHead, 
    GridTail,
    ShouldBeClosed,
    History,
    PendingLimit,
    PendingStop,
    LongInvestment,
    ShortInvestment
};

#define ROLES_COUNT  9


//+------------------------------------------------------------------+
//|   TYPE_TREND                                                     |
//+------------------------------------------------------------------+
enum TYPE_TREND
{
   LATERAL,  //Lateral
   UPPER,   //Ascending
   DOWN,    //Descending
};


enum ENUM_INDICATORS  
{
    NoIndicator,
    CandleIndicator,
    IshimokuIndicator,
    IchimokuRenkoIndicator,
    OsMAIndicator,   
    DefaultIndicator,
    HeikenAshiIndicator
};

enum Applied_price_ //Тип константы
{
PRICE_CLOSE_ = 1,     //Close
PRICE_OPEN_,          //Open
PRICE_HIGH_,          //High
PRICE_LOW_,           //Low
PRICE_MEDIAN_,        //Median Price (HL/2)
PRICE_TYPICAL_,       //Typical Price (HLC/3)
PRICE_WEIGHTED_,      //Weighted Close (HLCC/4)
PRICE_SIMPL_,         //Simple Price (OC/2)
PRICE_QUARTER_,       //Quarted Price (HLOC/4) 
PRICE_TRENDFOLLOW0_,  //TrendFollow_1 Price 
PRICE_TRENDFOLLOW1_,  //TrendFollow_2 Price 
PRICE_DEMARK_         //Demark Price
};

enum CROSS_TYPE
{
   CROSS_NO = 0,
   CROSS_DOWN = 1, 
   CROSS_UP = 2,
};


enum ENUM_SIGNALWEIGHTCALC 
{
    WeightByFilter,
    WeightBySignal,
    WeightBySum,
    WeightByMultiply,
    WeightByAND
};

enum ENUM_TRADE_PANEL_SIZE  
{
    PanelNormal,
    PanelSmall,
    PanelNone
};

#define DELETE_PTR(pointer)  if (pointer != NULL) { delete pointer; pointer = NULL; }

#define FAKE_MAGICNUMBER 1000000

class Constants {
public:

  static double GAP_VALUE;
  static string MTDATETIMEFORMAT;
  static string MYSQLDATETIMEFORMAT;
  static string SOLRDATETIMEFORMAT;
  static int SENTIMENTS_FETCH_PERIOD;
  static short MQL_PORT;
  static short AppService_PORT;
  static string JOBGROUP_TECHDETAIL;
  static string JOBGROUP_OPENPOSRATIO;
  static string JOBGROUP_EXECRULES;
  static string JOBGROUP_NEWS;
  static string JOBGROUP_THRIFT;
  static string CRON_MANUAL;
  static string SETTINGS_PROPERTY_BROKERSERVERTIMEZONE;
  static string SETTINGS_PROPERTY_PARSEHISTORY;
  static string SETTINGS_PROPERTY_STARTHISTORYDATE;
  static string SETTINGS_PROPERTY_USERTIMEZONE;
  static string SETTINGS_PROPERTY_NETSERVERPORT;
  static string SETTINGS_PROPERTY_ENDHISTORYDATE;
  //static string SETTINGS_PROPERTY_THRIFTPORT;
  static string SETTINGS_PROPERTY_INSTALLDIR;
  static string  SETTINGS_PROPERTY_RUNTERMINALUSER;
  static string PARAMS_SEPARATOR;
  static string LIST_SEPARATOR;
  static string GLOBAL_SECTION_NAME;
  
};

double Constants::GAP_VALUE = -125;
string Constants::MTDATETIMEFORMAT = "yyyy.MM.dd HH:mm";
string Constants::MYSQLDATETIMEFORMAT = "yyyy-MM-dd HH:mm:ss";

string Constants::SOLRDATETIMEFORMAT = "yyyy-MM-dd'T'HH:mm:ss'Z'";

int Constants::SENTIMENTS_FETCH_PERIOD = 100;

short Constants::MQL_PORT = 2020;

short Constants::AppService_PORT = 2020;

string Constants::JOBGROUP_TECHDETAIL = "Technical Details";

string Constants::JOBGROUP_OPENPOSRATIO = "Positions Ratio";

string Constants::JOBGROUP_EXECRULES = "Run Rules";

string Constants::JOBGROUP_NEWS = "News";

string Constants::JOBGROUP_THRIFT = "ThriftServer";

string Constants::CRON_MANUAL = "0 0 0 1 1 ? 2100";

string Constants::SETTINGS_PROPERTY_BROKERSERVERTIMEZONE = "BrokerServerTimeZone";

string Constants::SETTINGS_PROPERTY_PARSEHISTORY = "NewsEvent.ParseHistory";

string Constants::SETTINGS_PROPERTY_STARTHISTORYDATE = "NewsEvent.StartHistoryDate";

string Constants::SETTINGS_PROPERTY_USERTIMEZONE = "UserTimeZone";

string Constants::SETTINGS_PROPERTY_NETSERVERPORT = "FXMind.NETServerPort";

string Constants::SETTINGS_PROPERTY_ENDHISTORYDATE = "NewsEvent.EndHistoryDate";

//string Constants::SETTINGS_PROPERTY_THRIFTPORT = "FXMind.ThriftPort";

string Constants::SETTINGS_PROPERTY_INSTALLDIR = "InstallDir";

string Constants::SETTINGS_PROPERTY_RUNTERMINALUSER = "TerminalUser";

string Constants::PARAMS_SEPARATOR = "|";

string Constants::LIST_SEPARATOR = "~";

string Constants::GLOBAL_SECTION_NAME = "Global";

enum SignalFlags
{
   SignalToAuto = 0,
   SignalToServer = 1, 
   SignalToExpert = 2, 
   SignalToCluster = 3,
   SignalToTerminal = 4,
   SignalToAllTerminals = 5
};

enum ExpertMode
{
   LocalMode = 0,
   ClusterSlaveMode = 1, 
   ClusterMasterMode = 2 
};

//long const Constants::DEFAULT_MAGIC_NUMBER = 1000000;
enum SignalType
{   
   SIGNAL_FIRST = 1000,
   SIGNAL_CHECK_HEALTH = 1001,
   SIGNAL_DEALS_HISTORY = 1002,
   SIGNAL_CHECK_BALANCE = 1003,
   SIGNAL_UPDATE_RATES = 1004,
   SIGNAL_ACTIVE_ORDERS = 1005,
   SIGNAL_WARN_NEWS = 1006,
   SIGNAL_PENDING_ORDERS = 1008,
   SIGNAL_INIT_EXPERT = 1009,
   SIGNAL_DEINIT_EXPERT = 1010,
   SIGNAL_SAVE_EXPERT = 1011,
   SIGNAL_POST_TEXT = 1012,
   SIGNAL_POST_LOG = 1013,
   SIGNAL_UPDATE_EXPERT = 1014,   
   SIGNAL_MARKET_MANUAL_ORDER = 1015,
   SIGNAL_MARKET_EXPERT_ORDER = 1016,
   SIGNAL_MARKET_FROMPENDING_ORDER = 1017,
   SIGNAL_CLOSE_POSITION = 1018,
   SIGNAL_TODAYS_STAT = 1019,
   SIGNAL_INIT_TERMINAL = 1020,
   SIGNAL_DEINIT_TERMINAL = 1021,
   SIGNAL_CHECK_TRADEALLOWED = 1022,
   SIGNAL_LEVELS4SYMBOL = 1023,
   SIGNAL_STARTSERVER = 1024,
   SIGNAL_STOPSERVER = 1025,
   SIGNAL_GETMAINLOGPATH = 1026,
   SignalQuiet,
   SignalBUY, 
   SignalSELL, 
   SignalNEWS,
   SignalCANDLE,
   SignalCLOSEBUYPOS, 
   SignalCLOSESELLPOS,   
   SignalCLOSEALL,
   SIGNAL_LAST
};

#define SLEEP_DELAY_MSEC  1000

#define HTTP_FINCORE_TIMEOUT     20000 
#define HTTP_FINCORE_OP          300 
#define HTTP_FINCORE_TIMEOUT_INDI        3000

#define RetryOnErrorNumber  5

#define ISHIMOKU_PLAIN_NOTRADE 23

#define DEFAULT_MAGIC_NUMBER 1000000

#define CANDLE_PATTERN_MAXBARS   4

#define SL_PERCENTILE  0.75

#define TP_PERCENTILE  0.75

#define RISK_ATR 0.25

#define STOP_LUFT  1.02

// Max Amount in % you may loose daily
#define RISK_PER_DAY  0.02
// Mini daily gain that taken into account to do checks losses after gains
#define DAILY_MIN_GAIN   0.0065
// Losses in % after gains today
#define DAILY_GAIN_LOSS  0.3

// Current zero-based day of the week (0-Sunday,1,2,3,4,5,6).
#define NON_TRADING_DAY  3

//#define TP_PERCENTILEMIN  0.08


#define KEY_LEFT           37
#define KEY_UP             38
#define KEY_RIGHT          39
#define KEY_DOWN           40
#define KEY_NUMLOCK_DOWN   98
#define KEY_NUMLOCK_LEFT  100
#define KEY_NUMLOCK_RIGHT 102
#define KEY_NUMLOCK_UP    104


class SerializableEntity
{
protected:
public:
    CJAVal obj;
    void Deserialize(string val) {
      obj.Deserialize(val);      
    }
    
    SerializableEntity(string val) 
    {
        obj.Deserialize(val);
    }
    
    SerializableEntity() 
    {
    }
    
    virtual string ToString() 
    {
       return toString();
    }
    
    virtual string toString() {
        return obj.Serialize();
    }
    
    virtual CJAVal* Persistent() {
         return &obj;
    }
    
    virtual string Serialize() {
         return Persistent().Serialize();
    }
    
    template<typename T> void SaveVariable(string var_str, T value)
    {
       this.obj[var_str] = (T)value;
    }

    long LoadIntVariable(string var_str, int def_val)
    {
       if (obj.FindKey(var_str))
       {
          return obj[var_str].ToInt(); 
       }  
       return def_val;
    }
    
    double LoadDblVariable(string var_str, double def_val)
    {
       if (obj.FindKey(var_str))
       {
         if (obj[var_str].m_type == jtDBL)
             return obj[var_str].ToDbl(); 
         if (obj[var_str].m_type == jtINT) { 
             return (double)obj[var_str].ToInt();
         }
       }
       return def_val;
    }
    
    bool LoadBoolVariable(string var_str, bool def_val)
    {
       if (obj.FindKey(var_str))
       {
           return obj[var_str].ToBool(); 
       }
       return def_val;
    }
    
    string LoadStrVariable(string var_str, string def_val)
    {
       if (obj.FindKey(var_str))
       {
          return obj[var_str].ToStr(); 
       }
       return def_val;
    }
    
    void SetValue(string property, long value) {
         obj[property] = value;
    }
   
    void SetValue(string property, string value) {
         obj[property] = value;
    }
   
    void SetValue(string property, double value) {
         obj[property] = value;
    }
    
    void SetValue(string property, bool value) {
         obj[property] = value;
    }
    
    void SetJsonObject(string property, SerializableEntity& entity) 
    {
       // for single object
       obj[property].Set(entity.obj);
    }
    
    //void AddJsonArrayElement(string property, SerializableEntity& entity) 
    //{
       // For arrays
    //   obj[property].Add(entity.obj);
    //}
        
    long ValueLong(string property) {
        if (obj.FindKey(property))
           return (long)obj[property].ToDbl();
        return 0;
    }
    
    bool ValueBool(string property) {
        if (obj.FindKey(property))
           return obj[property].ToBool();
        return false;
    }
    
    double ValueDouble(string property) {
        if (obj.FindKey(property))
           return obj[property].ToDbl();
        return 0.0;
    }

    string ValueString(string property) {
        if (obj.FindKey(property))
           return obj[property].ToStr();
        return "";
    }
    
    /* string ValueObjectString(string property) {
        if (obj.FindKey(property))
           return obj[property].Serialize();
        return "";
    }*/
    
};

class ExpertParams : public SerializableEntity
{
public:
    long Account;
    string ChartTimeFrame;
    string Symbol;
    string EAName;
    long ObjectId;
    long Flags;
    string Reason;
    bool IsMaster;
    long Magic;

    virtual CJAVal* Persistent() {
        return &obj;
    }
    
    ExpertParams()
    {
    }
    
    ExpertParams(string fromJson)
    {
        obj.Deserialize(fromJson);
        if (obj.FindKey("Account"))
           Account = (long)obj["Account"].ToDbl();
        if (obj.FindKey("ChartTimeFrame"))
           ChartTimeFrame = obj["ChartTimeFrame"].ToStr();
        if (obj.FindKey("Symbol"))
           Symbol = obj["Symbol"].ToStr();           
        if (obj.FindKey("EAName"))
           EAName = obj["EAName"].ToStr();           
        if (obj.FindKey("ObjectId"))
           ObjectId = obj["ObjectId"].ToInt();           
        if (obj.FindKey("Magic"))
           Magic = obj["Magic"].ToInt();           
        if (obj.FindKey("Flags"))
           Flags = obj["Flags"].ToInt();           
        if (obj.FindKey("Reason"))
           Reason = obj["Reason"].ToStr();           
        if (obj.FindKey("IsMaster"))
           IsMaster = obj["IsMaster"].ToBool();           
    }
    
    void Fill(long account, ENUM_TIMEFRAMES period, string symbol, string EAName, long magic, int Reason, bool IsMaster) {
         this.obj["Account"] = account;
         string periodStr = EnumToString((ENUM_TIMEFRAMES)period);
         this.obj["ChartTimeFrame"] = periodStr;
         this.obj["Symbol"] = symbol;
         this.obj["EAName"] = EAName;
         this.obj["ObjectId"] = magic;
         this.obj["Flags"] = 0;
         this.obj["Reason"] = IntegerToString(Reason);
         this.obj["IsMaster"] = IsMaster;
         this.obj["Magic"] = Magic;         
    }
};

