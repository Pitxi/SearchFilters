# SearchFilters

This library allows the use of complex filtering with Entity Framework Core by extending IQueryable functionality.

# Usage Example

```C#
var startDate   = DateTime.Now.AddYears(-20);
var endDate     = DateTime.Now;
var NameFilter  = new DataFilter<string> { ComparisonName = "contains", Values = new[] { "Xavier" } };
var MoneyFilter = new DataFilter<decimal> { ComparisonName = "less-than", Values = new[] { 1000 } };
var DateFilter  = new DataFilter<DateTime> { ComparisonName = "is-in-range", Values = new DateTime[] { startDate, endDate } };

_dbContext.Players
          .Filter(p => p.Money, MoneyFilter)
          .Filter(p => p.Name, NameFilter)
          .Filter(p => p.Birthdate, DateFilter, new FilterOptions { IgnoreTime = true })
          .ToList();
```
The code above will filter the set of Players to obtain only those whose name contains the string 
```"Xavier"```, have less than 1000 coins and were born between 20 years ago and today.

Entity Framework Core should create the required SQL and let the database do the filtering for us.

# FilterOptions
The ```FilterOptions``` object changes the behaviour of the filtering process.
Right now only one option exists, but more will be added as needed to allow more flexibility.

* ```IgnoreTime```: This option makes the DateTime filtering ignore the time part of the ```DateTime``` structs.
