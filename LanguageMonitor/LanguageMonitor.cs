/**
 * Plugin: Language Monitor
 * Author(s): Ciavi, ZionMistaken
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using PRoCon.Core;
using PRoCon.Core.Players;
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

        protected MySqlConnection _connection;
        protected MySqlConnectionStringBuilder _string;
        protected bool _connected = false;

        protected List<CPlayerInfo> _players;
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
                case "Schema":
                    _schema = strValue;
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
            _schema = string.Empty;
            _username = string.Empty;
            _password = string.Empty;
            _regex = new List<string>();
            _players = new List<CPlayerInfo>();
        }

        public void Enable()
        {
            ExecuteCommand("procon.protected.plugins.enable", GetType().Name, "True");
        }

        public void Disable()
        {
            ExecuteCommand("procon.protected.plugins.enable", GetType().Name, "False");
        }

        public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion)
        {
            RegisterEvents(GetType().Name, "OnPluginLoaded", "OnPluginEnabled", "OnPluginDisabled",
                "OnAccountLogin", "OnListPlayers", "OnPlayerJoin", "OnPlayerKilled", "OnPlayerSpawned",
                "OnRoundOver", "OnPlayerLeft", "OnGlobalChat", "OnTeamChat", "OnSquadChat");
        }

        public void OnPluginEnable()
        {
            _enabled = true;

            try
            {
                InitializeSchema();
                Console("^2Enabled.");
            }
            catch (DataNotProvidedException m)
            {
                Console($@"^2{m.Message}");
                Disable();
            }
            catch (Exception e)
            {
                Console($@"^2{e.Message}");
                Console($@"^2{e.StackTrace}");
                Disable();
            }
        }

        public void OnPluginDisable()
        {
            _enabled = false;

            Console("^1Disabled.");
        }

        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset)
        {
            if (subset.Subset == CPlayerSubset.PlayerSubsetType.All)
                _players = players;
        }

        private void Console(string message)
        {
            ExecuteCommand("procon.protected.pluginconsole.write", "[Language Monitor] " + message);
        }

        #region Database Management
        public void InitializeSchema()
        {
            BuildConnectionString();
            Connect();

            if (!TableExists("tbl_language_infractions"))
                CreateTable("tbl_language_infractions",
                    new Dictionary<string, string[]>() { 
                        { "id", new string[]{ "int", "not null", "auto_increment" } },
                        { "ea_guid", new string[]{ "varchar(35)", "not null" } },
                        { "message", new string[]{ "text", "not null" } },
                        { "inflicted_on", new string[]{ "timestamp", "not null", "default current_timestamp" } },
                        { "forgiven", new string[]{ "bit", "not null", "default false" } },
                        { "forgiven_by", new string[]{ "int" } },
                        { "forgiven_on", new string[]{ "timestamp" } },
                        { "primary", new string[]{ "key(id)" } },
                        { "foreign", new string[]{ "key(ea_guid)", "references", "tbl_playerdata(EA_GUID)" } },
                    });

            Disconnect();
        }

        private void BuildConnectionString()
        {
            if (_hostname == string.Empty || _schema == string.Empty
                || _username == string.Empty || _password == string.Empty)
                throw new DataNotProvidedException();

            _string = new MySqlConnectionStringBuilder()
            {
                Server = _hostname,
                Port = (uint)_port,
                Database = _schema,
                UserID = _username,
                Password = _password
            };
        }

        private bool TableExists(string tableName)
        {
            string query = $@"select count(*) from information_schema.tables where
                table_schema = '{_schema}' and table_name = '{tableName}';";

            MySqlCommand _command = new MySqlCommand(query, _connection);
            return Convert.ToInt32(_command.ExecuteScalar()) > 0;
        }

        private void CreateTable(string tableName, Dictionary<string, string[]> columns)
        {
            string query = $@"create table {tableName}(";

            foreach (var column in columns)
            {
                string columnName = column.Key;
                string columnDef = string.Join(" ", column.Value);
                query += columnName + " " + columnDef + ",";
            }

            query = query.Substring(0, query.Length - 1);
            query += ");";

            MySqlCommand _command = new MySqlCommand(query, _connection);
            _command.ExecuteNonQuery();
        }

        private void Connect()
        {
            _connection = new MySqlConnection(_string.GetConnectionString(true));
            _connected = true;
            _connection.Open();
        }

        private void Disconnect()
        {
            if (_connection != null && _connected)
                _connection.Close();
        }
        #endregion
    }

    public class DataNotProvidedException : Exception
    {
        public override string Message
        {
            get
            {
                return "Mandatory data was not provided";
            }
        }
    }
}