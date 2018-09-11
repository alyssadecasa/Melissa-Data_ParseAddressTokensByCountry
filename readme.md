# Parsed Address Tokens By Country Script
## By Alyssa House

### About this project
This is a C# script written for Melissa Data's Country Sergeant project. It's purpose is to update a SQLite database containing two fields, words and countryLists, respectfully. The words are parsed address tokens that appear in 12 million address records and the countryList is the list of countries that each word appears in. This SQLite database is used in the Country Sergeant project as the data file. 

### About the Parent Project, Country Sergeant
The purpose of the Country Sergeant project is to determine the country of an inputted address. It determines this by parsing the inputted address into words and comparing those words to the words in the SQLite database. For each word that matches, the associated countryList is gotten and each of the countries are counted. At the end, the program determines  the country for which the most parsed address tokens were associated with, and it returns the country code.