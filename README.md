# UltraMapper.Parsing
Common classes and interfaces for parsers based on UltraMapper.

Provides a common way to map data to strong-typed object using UltraMapper,
effectively decoupling the string-analysis stage and the mapping-stage of any parser.

If you need to implement a parser the only thing you need to do is 
reading your data into a IParsedParam, and then pass it to UltraMapper.
