# UltraMapper.Parsing

A parser is a software component that takes input data (frequently text) and builds a data structure.

UltraMapper.Parsing is an extension to UltraMapper which provides a parse-tree structure 
and a way to generate parse-tree to strong-type object code.

This allows effective decoupling of the string-analysis and the mapping stage of any parser;
which in turn allows you to write a complete parser by only implementing a string-analysis code.

If you need to implement a parser the only thing you need to do is 
reading your data into a IParsedParam, and then pass it to UltraMapper.
