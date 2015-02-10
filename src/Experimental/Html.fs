﻿module Suave.Html

open System

type Attribute = string * string

/// Representation of the things that go into an HTML element
type Element =
  /// The element itself; a name, a xml namespace and an array of attribute-value pairs.
  | Element of string * string * Attribute[]
  /// A text element inside the HTML element
  | Text of string
  /// Whitespace for formatting
  | WhiteSpace of string

/// XML is a list of nodes
type Xml = Xml of Node list
/// Each node has/is an element and then some other XML
and Node = Element * Xml

let tag tag attr (contents : Xml) = Xml [ (Element (tag,"", attr), contents) ]

let empty = Xml[]

let text s = Xml([Text s, Xml[]])

let empty_text = text ""

/// HTML elements.
/// If you need to pass attributes use the version sufixed by ` (funny quote symbol)

/// Flattens an XML list
let flatten xs = xs |> List.map (fun (Xml y) -> y) |> List.concat |> Xml


let htmlAttr attr s = tag "html" (Array.ofList attr) s
let html xs = htmlAttr [ ] (flatten xs)

let headAttr attr s = tag "head" (Array.ofList attr) s
let head xs = headAttr [ ] (flatten xs)

let titleAttr attr s = tag "title" (Array.ofList attr) (Xml([Text s,Xml []]))
let title  = titleAttr [ ]

let linkAttr attr = tag "link" (Array.ofList attr) empty
let link  = linkAttr [ ]

let scriptAttr attr x = tag "script" (Array.ofList attr) (flatten x)
let script  = scriptAttr [ ]

let bodyAttr attr x = tag "body" (Array.ofList attr) (flatten x)
let body  = bodyAttr [ ]

let divAttr attr x = tag "div" (Array.ofList attr) (flatten x)
let div  = divAttr [ ]

let pAttr attr x = tag "p" (Array.ofList attr) (flatten x)
let p  = pAttr [ ]

let spanAttr attr x = tag "span" (Array.ofList attr) x
let span  = spanAttr [ ]

let imgAttr attr = tag "img" (Array.ofList attr) empty
let img  = imgAttr [ ]

let brAttr attr = tag "br" (Array.ofList attr) empty
let br = brAttr [ ]

let inputAttr attr = tag "input" (Array.ofList attr) empty
let input = inputAttr [ ]

/// Example

let sample_page = 
  html [ 
    head [
      title "Little HTML DSL"
      linkAttr [ "rel", "https://instabt.com/instaBT.ico" ]
      scriptAttr [ "type", "text/javascript"; "src", "js/jquery-2.1.0.min.js" ] []
      scriptAttr [ "type", "text/javascript" ] [ text "$().ready(function () { setup(); });" ]
    ] 
    body [
      divAttr ["id","content"] [ 
        p [ text "Hello world." ]
        br
        imgAttr [ "src", "http://fsharp.org/img/logo/fsharp256.png"]
      ]
    ]
 ]

/// Rendering

let internal leaf_element_to_string = function
 | Text text -> text
 | WhiteSpace text -> text
 | Element (e,id, attributes) ->
   if Array.length attributes = 0 then
     sprintf "<%s/>" e
   else
     let ats = 
       Array.map (fun (a,b) -> sprintf "%s=\"%s\"" a b) attributes
       |> String.Concat
     sprintf "<%s %s/>" e ats

let internal begin_element_to_string = function
 | Text text -> failwith "invalid option"
 | WhiteSpace text -> failwith "invalid option"
 | Element (e,id, attributes) ->
   if Array.length attributes = 0 then
     sprintf "<%s>" e
   else
     let ats = 
       Array.map (fun (a,b) -> sprintf "%s=\"%s\"" a b) attributes
       |> String.Concat
     sprintf "<%s %s>" e ats

let internal end_element_to_string = function
 | Text text -> failwith "invalid option"
 | WhiteSpace text -> failwith "invalid option"
 | Element (e,_, _) ->
   sprintf "</%s>" e

let rec internal node_to_string (element : Element, xml) =
  match xml with
  | Xml [] -> leaf_element_to_string element
  | _  ->
    let inner = xml_to_string xml
    (begin_element_to_string element) + inner + (end_element_to_string element)

and xml_to_string (Xml xml) =
  String.Concat (List.map node_to_string xml)

///
/// let html = sample_page |> xml_to_string
///