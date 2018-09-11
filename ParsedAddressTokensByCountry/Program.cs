using System;
using System.Text;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace ParsedAddressTokensByCountry
{
    class Program
    {
        static void Main(string[] args)
        {
            // set encoding
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            IDictionary<string, string> currentEntries = BuildCurrentEntriesDictionary();
            IDictionary<string, string> unevaluatedEntries = BuildUnevaluatedEntriesDictionary();
            IDictionary<string, string> newWordEntries = new Dictionary<string, string>();
            IDictionary<string, string> newCountryEntries = new Dictionary<string, string>();

            BuildNewEntriesDictionaries(currentEntries, unevaluatedEntries, newWordEntries, newCountryEntries);

            UpdateCountriesInSQLite(newCountryEntries);

            InsertWordsInSQLite(newWordEntries);

            // Keep Console open
            Console.WriteLine("program finished executing");
            Console.Read();
        }

        /// <summary>
        /// Builds Dictionary containing the current word, countryList entries from the SQLite countryWords2 table
        /// </summary>
        /// <returns>Dictionary of current word, countryList entries</returns>
        static IDictionary<string, string> BuildCurrentEntriesDictionary()
        {
            IDictionary<string, string> currentEntries = new Dictionary<string, string>();

            // make call to sqlite
            string readInTable = "countryWords2";
            using (SqliteDataReader reader = ReadInSqliteRecords(readInTable))
            {
                while (reader.Read())
                {
                    currentEntries.Add(reader["word"].ToString().ToUpper().Trim(), reader["countryList"].ToString().Trim());
                }
                reader.Close();
            }
            foreach (KeyValuePair<string, string> entry in currentEntries)
            {
                if (entry.Value.Contains("|"))
                    Console.WriteLine(entry.Key + "," + entry.Value);
            }
            return currentEntries;
        }

        /// <summary>
        /// Builds Dictionary containing the unevaluated word, countryList entries from the SQL 
        /// InternWorkspace.dbo.GeoPC_201804 table
        /// </summary>
        /// <returns>Dictionary of unevaluated word, countryList entries</returns>
        static IDictionary<string, string> BuildUnevaluatedEntriesDictionary()
        {
            IDictionary<string, string> unevaluatedWordsDictionary = new Dictionary<string, string>();
            List<SqlRecordObj> unparsedRecords = new List<SqlRecordObj>();

            // make call to SQL and read in records
            string readInTable = "InternWorkspace.dbo.GeoPC_201804";
            using (SqlDataReader reader = ReadInSQLRecords(readInTable))
            {
                // create Regex for removing excess whitespace
                Regex excessWhitespace = new Regex("[ ]{2,}", RegexOptions.None);

                while (reader.Read()) // for each row
                {
                    // Combine all columns into a single string
                    string unparsedWords = reader["region1"] + " " + reader["region2"] + " " + reader["region3"] + " "
                        + reader["region4"] + " " + reader["locality"] + " " + reader["postcode"] + " " + reader["suburb"];
                    unparsedWords = excessWhitespace.Replace(unparsedWords, " ").Trim();
                    unparsedWords = unparsedWords.ToUpper();

                    // Build list of SQL records with a country and a string of unparsed words
                    unparsedRecords.Add(new SqlRecordObj(reader["iso"].ToString().Trim(), unparsedWords));
                }
            }

            foreach (SqlRecordObj record in unparsedRecords)
            {
                foreach (string word in record.UnparsedWords.Split(" "))
                {
                    if (unevaluatedWordsDictionary.ContainsKey(word))
                    {
                        if (!unevaluatedWordsDictionary[word].Contains(record.CountryCode))
                        {
                            unevaluatedWordsDictionary[word] = unevaluatedWordsDictionary[word] + "|" + record.CountryCode;
                        }
                    }
                    else
                    {
                        unevaluatedWordsDictionary.Add(word.ToUpper(), record.CountryCode);
                    }
                }
            }
            return unevaluatedWordsDictionary;
        }

        /// <summary>
        /// Builds Dictionary containing the new word, countryList entries to be added to the 
        /// SQLite table
        /// </summary>
        /// <param name="currentEntries">current entries in SQLite table</param>
        /// <param name="unevaluatedEntries">unevaluated entries to be added to SQLite table</param>
        /// <param name="newWordEntries">entries containing new words to be added as new entries to the SQLite table</param>
        /// <param name="newCountryEntries">entries containing new countries to be added to prexisting entries in the SQLite table</param>
        static void BuildNewEntriesDictionaries(
            IDictionary<string, string> currentEntries, IDictionary<string, string> unevaluatedEntries,
            IDictionary<string, string> newWordEntries, IDictionary<string, string> newCountryEntries)
        {
            foreach (KeyValuePair<string, string> entry in unevaluatedEntries)
            {
                if (currentEntries.ContainsKey(entry.Key))
                {
                    // check if current parsed word contains the countries from the current entry. If not, 
                    // add entry to newCountryEntries
                    string[] countries = entry.Value.Split("|");
                    bool updated = false;
                    for (int i = 0; i < countries.Length; i++)
                    {
                        if (!currentEntries[entry.Key].Contains(countries[i]))
                        {
                            currentEntries[entry.Key] = currentEntries[entry.Key] + "|" + countries[i];
                            Console.WriteLine("updated countrylist: " + entry.Key + ", " + currentEntries[entry.Key]);
                            updated = true;
                        }
                    }
                    if (updated)
                        newCountryEntries.Add(entry.Key, currentEntries[entry.Key]);
                }
                else
                {
                    currentEntries.Add(entry.Key, entry.Value);
                    newWordEntries.Add(entry.Key, entry.Value);
                }
            }
        }

        /// <summary>
        /// Updates countryLists in the SQLite table
        /// </summary>
        /// <param name="newCountryEntries">entries containing new countries to be added to prexisting entries in the SQLite table</param>
        static void UpdateCountriesInSQLite(IDictionary<string, string> newCountryEntries)
        {
            // create connection
            SqliteConnectionStringBuilder connString = new SqliteConnectionStringBuilder(@"Data Source = countrySergeant.db3");
            using (SqliteConnection connection = new SqliteConnection(connString.ToString()))
            {
                StringBuilder query = new StringBuilder("UPDATE countryWords2 ");
                foreach (KeyValuePair<string, string> entry in newCountryEntries)
                {
                    query.Append("SET countryList = \'" + entry.Value + "\' WHERE word = \'" + entry.Key.Replace("'", "''") + "\' ");
                    Console.WriteLine(query);

                    // execute query
                    using (SqliteCommand command = new SqliteCommand(query.ToString(), connection))
                    {
                        command.Connection.Open();
                        command.ExecuteNonQuery();
                    }
                    query.Remove(21, query.Length - 21);
                }
            }
        }

        /// <summary>
        /// Adds new records to the SQLite table
        /// </summary>
        /// <param name="newWordEntries">entries containing new words to be added as new entries to the SQLite table</param>
        static void InsertWordsInSQLite(IDictionary<string, string> newWordEntries)
        {
            StringBuilder query = new StringBuilder("INSERT INTO countryWords2 (word, countryList) VALUES ");
            foreach (KeyValuePair<string, string> entry in newWordEntries)
            {
                query.Append("(\'" + entry.Key.Replace("'","''") + "\', \'" + entry.Value + "\'), ");
            }
            query.Remove(query.Length - 2, 2);
            query.Append(";");
            Console.WriteLine(query);

            // create connection
            SqliteConnectionStringBuilder connString = new SqliteConnectionStringBuilder(@"Data Source = countrySergeant.db3");
            SqliteConnection connection = new SqliteConnection(connString.ToString());
            connection.Open();

            // execute query
            using (SqliteCommand command = new SqliteCommand(query.ToString(), connection))
            {
                command.Connection.Open();
                command.ExecuteNonQuery();
                command.Connection.Close();
            }
        }

        //----------------------------------------------------------------------------------
        // Private Helper Methods
        //----------------------------------------------------------------------------------
        /// <summary>
        /// Private helper method that reads in SQL Records for the BuildUnevaluatedEntriesDictionary method
        /// </summary>
        /// <param name="sqlReadInTable">input SQL table</param>
        /// <returns>SqlDataReader reader to read the entries returned from the SQL input table</returns>
        private static SqlDataReader ReadInSQLRecords(string sqlReadInTable)
        {
            // create connection string
            SqlConnectionStringBuilder connString = new SqlConnectionStringBuilder();
            BuildSqlConnectionString(connString);

            // create connection
            SqlConnection connection = new SqlConnection(connString.ToString());

            // set query
            string query = "SELECT iso, region1, region2, region3, region4, locality, postcode, suburb FROM " + sqlReadInTable;

            using (SqlCommand command = new SqlCommand(query, connection))
            {
                command.Connection.Open();
                SqlDataReader reader = command.ExecuteReader();
                return reader;
            }
        }

        /// <summary>
        /// Private helper method that builds a SQL Connection String for the BuildUnevaluatedEntriesDictionary method
        /// </summary>
        /// <param name="connString">connection string to be built</param>
        /// <returns>SqlConnectionStringBuilder connString to connect to the SQL database</returns>
        private static SqlConnectionStringBuilder BuildSqlConnectionString(
            SqlConnectionStringBuilder connString)
        {
            connString.DataSource = "Brown11";              // Server
            connString.InitialCatalog = "InternWorkspace";  // Database
            connString.IntegratedSecurity = true;           // Connection type: Integrated Security
            return connString;
        }

        /// <summary>
        /// Private helper method that reads in SQLite Records for the BuildCurrentEntriesDictionary method
        /// </summary>
        /// <param name="ReadInTable">input SQLite table</param>
        /// <returns>SqliteDataReader reader to read in the entries returned from the SQLite input table</returns>
        private static SqliteDataReader ReadInSqliteRecords(string ReadInTable)
        {
            // create connection
            SqliteConnectionStringBuilder connString = new SqliteConnectionStringBuilder(@"Data Source = countrySergeant.db3");
            SqliteConnection connection = new SqliteConnection(connString.ToString());
            connection.Open();

            // create and execute query
            string query = "SELECT word, countryList FROM " + ReadInTable;
            SqliteCommand command = new SqliteCommand(query, connection);
            command.Connection.Open();

            SqliteDataReader reader = command.ExecuteReader();
            return reader;
        }
    }
}
