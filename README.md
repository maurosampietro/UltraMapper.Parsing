# UltraMapper.Parsing

A parser is a software component that takes input data (frequently text) and builds a data structure.

UltraMapper.Parsing is an extension to UltraMapper which provides a parse-tree structure (a general representation of your data)
and a way to generate code mapping that general structure to your strong-type object.

This effectively allows the decoupling of the string-analysis stage and the mapping stage of any parser;
which in turn allows you to write a complete parser by only implementing the string-analysis part.

If you need to implement a parser the only thing you need to do is 
reading your data into a IParsedParam, and then pass it to UltraMapper.
