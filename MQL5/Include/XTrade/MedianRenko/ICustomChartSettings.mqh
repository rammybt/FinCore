#include <XTrade\MedianRenko\CommonSettings.mqh>
#include <XTrade\MedianRenko\CustomChartInputs.mqh>

interface ICustomChartSettings
{
   int GetHandle();
   void Set();
   void Save();
   bool Load();
   void Delete();
   bool Changed(double currentRuntimeId); 

   #ifdef USE_CUSTOM_SYMBOL
      CUSTOM_SYMBOL_SETTINGS     GetCustomSymbolSettings();
   #else   
      ALERT_INFO_SETTINGS        GetAlertInfoSettings(void);
      CHART_INDICATOR_SETTINGS   GetChartIndicatorSettings(void);   
   #endif
};
