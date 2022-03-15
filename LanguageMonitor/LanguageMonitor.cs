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
        protected List<string> _factors;
        protected List<Rule> _rules;
        protected enumBoolYesNo _excludeadmins;

        protected bool _enabled = false;

        protected MySqlConnection _connection;
        protected MySqlConnectionStringBuilder _string;
        protected bool _connected = false;

        protected List<CPlayerInfo> _players;
        #endregion

        #region VariablesManagement
        public List<CPluginVariable> GetPluginVariables()
        {
            try
            {
                List<CPluginVariable> pluginVariables = new List<CPluginVariable>()
                {
                    new CPluginVariable("Monitor|Regular Expressions", typeof(string[]), _regex.ToArray()),
                    new CPluginVariable("Monitor|Factors", typeof(string[]), _factors.ToArray()),
                    new CPluginVariable("Monitor|Exclude Administrators", typeof(enumBoolYesNo), _excludeadmins),
                    new CPluginVariable("Database|Hostname", typeof(string), _hostname),
                    new CPluginVariable("Database|Port", typeof(int), _port),
                    new CPluginVariable("Database|Schema", typeof(string), _schema),
                    new CPluginVariable("Database|Username", typeof(string), _username),
                    new CPluginVariable("Database|Password", typeof(string), _password),
                };

                return pluginVariables;
            }
            catch (Exception e)
            {
                Console($@"^2{e.Message}");
                Console($@"^2{e.StackTrace}");
                return new List<CPluginVariable>();
            }
        }

        public List<CPluginVariable> GetDisplayPluginVariables()
        {
            try
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
                        pluginVariables.Add(new CPluginVariable("Monitor|Factors", typeof(string[]), _factors.ToArray()));
                        pluginVariables.Add(new CPluginVariable("Monitor|Exclude Administrators", typeof(enumBoolYesNo), _excludeadmins));
                    }

                    return pluginVariables;
            }
            catch (Exception e)
            {
                Console($@"^2{e.Message}");
                Console($@"^2{e.StackTrace}");
                return new List<CPluginVariable>();
            }
        }

        public void SetPluginVariable(string strVariable, string strValue)
        {
            switch (strVariable)
            {
                case "Regular Expressions":
                    _regex = new List<string>(CPluginVariable.DecodeStringArray(strValue));
                    BuildRules();
                    break;
                case "Factors":
                    _factors = new List<string>(CPluginVariable.DecodeStringArray(strValue));
                    BuildRules();
                    break;
                case "Exclude Administrators":
                    _excludeadmins = (strValue == enumBoolYesNo.Yes.ToString() ? enumBoolYesNo.Yes : enumBoolYesNo.No);
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
            _factors = new List<string>();
            _rules = new List<Rule>();
            _excludeadmins = enumBoolYesNo.Yes;
            _players = new List<CPlayerInfo>();
        }

        /// <summary>
        /// Enables the Plugin.
        /// </summary>
        public void Enable()
        {
            ExecuteCommand("procon.protected.plugins.enable", GetType().Name, "True");
        }

        /// <summary>
        /// Disables the Plugin.
        /// </summary>
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

        public override void OnGlobalChat(string speaker, string message)
        {
            if (Evaluate(message))
                Punish(speaker);
        }

        public override void OnTeamChat(string speaker, string message, int teamId)
        {
            base.OnTeamChat(speaker, message, teamId);
        }

        public override void OnSquadChat(string speaker, string message, int teamId, int squadId)
        {
            base.OnSquadChat(speaker, message, teamId, squadId);
        }

        /// <summary>
        /// Writes to the Plugin Console.
        /// </summary>
        /// <param name="message">
        /// Message to be written.
        /// </param>
        private void Console(string message)
        {
            ExecuteCommand("procon.protected.pluginconsole.write", $@"[Language Monitor] {message}");
        }

        /// <summary>
        /// Builds the rules for the plugin to follow.
        /// </summary>
        private void BuildRules()
        {
            int diff = Math.Abs(_regex.Count - _factors.Count);

            for (int i = 0; i < diff; i++)
            {
                if (_regex.Count > _factors.Count)
                    _factors.Add(1.00.ToString());
                else
                    _factors.Remove(_factors[i]);
            }

            _rules.Clear();

            for (int i = 0; i < _factors.Count; i++)
            {
                if (!double.TryParse(_factors[i], out double d))
                    d = 0.00;
                _rules.Add(new Rule(d, _regex[i]));
            }
        }

        /// <summary>
        /// Returns the player's EA_GUID from current PlayerList or, if the player is still not in it, from the Database.
        /// </summary>
        /// <param name="soldierName">
        /// Player's name.
        /// </param>
        /// <returns></returns>
        private string GetPlayerGUID(string soldierName)
        {
            return _players.Find((p) => p.SoldierName == soldierName).GUID ??
                GetColumnWhere("tbl_playerdata", "EAGUID", new Dictionary<string, object[]>() {
                    { "=", new object[] { "SoldierName", soldierName } },
                }).ToArray()[0].ToString();
        }

        private bool Evaluate(string text)
        {
            Regex master = new Regex(String.Join("|", _regex.ToArray()));
            return master.IsMatch(text);
        }

        private void Punish(string soldierName)
        {
            ExecuteCommand("admin.kill", soldierName);
        }

        #region Database Management
        /// <summary>
        /// Initializes the Database Schema.
        /// </summary>
        public void InitializeSchema()
        {
            BuildConnectionString();

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
                        { "foreign", new string[]{ "key(ea_guid)", "references", "tbl_playerdata(EAGUID)" } },
                    });
        }

        /// <summary>
        /// Builds the Connection String for the Database.
        /// </summary>
        /// <exception cref="DataNotProvidedException">
        /// Not all the necessary data was provided in order to build the Connection String.
        /// </exception>
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

        /// <summary>
        /// Checks if the Table exists on the current Schema.
        /// </summary>
        /// <param name="tableName">
        /// Table name.
        /// </param>
        /// <returns>
        /// Was the table found?
        /// </returns>
        private bool TableExists(string tableName)
        {
            Connect();

            string query = $@"select count(*) from information_schema.tables where
                table_schema = '{_schema}' and table_name = '{tableName}';";

            MySqlCommand _command = new MySqlCommand(query, _connection);
            bool returnValue = Convert.ToInt32(_command.ExecuteScalar()) > 0;

            Disconnect();

            return returnValue;
        }

        /// <summary>
        /// Creates a Table on the current Schema.
        /// </summary>
        /// <param name="tableName">
        /// Table name.
        /// </param>
        /// <param name="columns">
        /// A collection of Columns, composed by name and attributes.
        /// </param>
        private void CreateTable(string tableName, Dictionary<string, string[]> columns)
        {
            Connect();

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

            Disconnect();
        }

        /// <summary>
        /// Gets the Column values where the specified condition(s) are valid.
        /// </summary>
        /// <param name="tableName">
        /// Table name.
        /// </param>
        /// <param name="columnName">
        /// Column name.
        /// </param>
        /// <param name="whereConditions">
        /// A collection of Conditions, composed by operator and values.
        /// </param>
        /// <returns></returns>
        private List<object> GetColumnWhere(string tableName, string columnName, Dictionary<string, object[]> whereConditions)
        {
            Connect();

            string query = $@"select {columnName} from {tableName} where ";

            foreach (var whereCondition in whereConditions)
            {
                string fullCondition = $@"{(string)whereCondition.Value[0]} {whereCondition.Key} {(string)whereCondition.Value[1]} and ";
                query += fullCondition;
            }

            query = query.Substring(0, query.Length - 5);
            query += ";";

            MySqlCommand _command = new MySqlCommand(query, _connection);
            MySqlDataReader _reader = _command.ExecuteReader();

            List<object> returnValue = new List<object>();

            while (_reader.Read())
            {
                returnValue.Add(_reader.GetValue(0));
            }

            Disconnect();

            return returnValue;
        }

        /// <summary>
        /// Connects to the Database.
        /// </summary>
        private void Connect()
        {
            _connection = new MySqlConnection(_string.GetConnectionString(true));
            _connected = true;
            _connection.Open();
        }

        /// <summary>
        /// Disconnects from the Database.
        /// </summary>
        private void Disconnect()
        {
            if (_connection != null && _connected)
                _connection.Close();
            _connected = false;
        }
        #endregion
    }

    public class Rule
    {
        public double Factor { get; set; }
        public string Regex { get; set; }

        public Rule(double factor, string regex)
        {
            Factor = factor;
            Regex = regex;
        }
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