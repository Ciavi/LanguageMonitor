/**
 * Plugin: Language Monitor
 * Author(s): Ciavi, ZionMistaken
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using PRoCon.Core;
using PRoCon.Core.Plugin;

using MySql;
using MySql.Data;
using MySql.Data.MySqlClient;
using MySql.Data.Types;

namespace PRoConEvents
{
    public class LanguageMonitor : PRoConPluginAPI, IPRoConPluginInterface
    {
        #region Variables
        protected string _hostname;
        protected int _port;
        protected string _schema;
        protected string _username;
        protected string _password;
        protected List<string> _regex;

        protected bool _enabled = false;
        #endregion

        #region VariablesManagement
        public List<CPluginVariable> GetPluginVariables()
        {
            List<CPluginVariable> pluginVariables = new List<CPluginVariable>()
            {
                new CPluginVariable("Monitor|Regular Expressions", typeof(string[]), _regex.ToArray()),
                new CPluginVariable("Database|Hostname", typeof(string), _hostname),
                new CPluginVariable("Database|Port", typeof(int), _port),
                new CPluginVariable("Database|Schema", typeof(string), _schema),
                new CPluginVariable("Database|Username", typeof(string), _username),
                new CPluginVariable("Database|Password", typeof(string), _password),
            };

            return pluginVariables;
        }

        public List<CPluginVariable> GetDisplayPluginVariables()
        {
            List<CPluginVariable> pluginVariables = new List<CPluginVariable>
            {
                new CPluginVariable("Database|Hostname", typeof(string), _hostname)
            };

            if (_hostname != string.Empty)
            {
                pluginVariables.Add(new CPluginVariable("Database|Port", typeof(int), _port));
                pluginVariables.Add(new CPluginVariable("Database|Schema", typeof(string), _schema));
                pluginVariables.Add(new CPluginVariable("Database|Username", typeof(string), _username));
                pluginVariables.Add(new CPluginVariable("Database|Password", typeof(string), _password));
                pluginVariables.Add(new CPluginVariable("Monitor|Regular Expressions", typeof(string[]), _regex.ToArray()));
            }


            return pluginVariables;
        }

        public void SetPluginVariable(string strVariable, string strValue)
        {
            switch (strVariable)
            {
                case "Regular Expressions":
                    _regex = new List<string>(CPluginVariable.DecodeStringArray(strValue));
                    break;
                case "Hostname":
                    _hostname = strValue;
                    break;
                case "Port":
                    int port;
                    if (int.TryParse(strValue, out port))
                        _port = port;
                    else _port = 3306;
                    break;
                case "Username":
                    _username = strValue;
                    break;
                case "Password":
                    _password = strValue;
                    break;
                default: break;
            }
        }
        #endregion

        #region PluginInfo
        public string GetPluginName()
        {
            return "Language Monitor";
        }

        public string GetPluginVersion()
        {
            return "0.0.1-dev";
        }

        public string GetPluginAuthor()
        {
            return "Ciavi, ZionMistaken";
        }

        public string GetPluginWebsite()
        {
            return @"github.com/Ciavi/language-monitor";
        }

        public string GetPluginDescription()
        {
            return @"Super simple database-based language enforcer plugin for PRoCon.";
        }
        #endregion

        public LanguageMonitor()
        {
            _hostname = string.Empty;
            _port = 3306;
            _username = string.Empty;
            _password = string.Empty;
            _regex = new List<string>();
        }

        public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion)
        {
            RegisterEvents(GetType().Name, "OnPluginLoaded", "OnPluginEnabled", "OnPluginDisabled");
        }

        public void OnPluginEnable()
        {
            _enabled = true;

            Console("^bLanguage Monitor ^2Enabled.");
        }

        public void OnPluginDisable()
        {
            _enabled = false;

            Console("^bLanguage Monitor ^1Disabled.");
        }

        public void Console(string message)
        {
            ExecuteCommand("procon.protected.pluginconsole.write", message);
        }
    }
}