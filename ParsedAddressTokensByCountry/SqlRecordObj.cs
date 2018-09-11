using System;
using System.Collections.Generic;
using System.Text;

namespace ParsedAddressTokensByCountry
{
    class SqlRecordObj
    {
        public SqlRecordObj(string countryCode, string unparsedWords)
        {
            CountryCode = countryCode;
            UnparsedWords = unparsedWords;
        } 
        public string CountryCode { get; set; }
        public string UnparsedWords { get; set; }
    }
}
