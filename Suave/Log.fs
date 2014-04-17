﻿module Suave.Log

open System
open System.Diagnostics

open Suave.Utils.RandomExtensions

/// The log levels specify the severity of the message.
[<CustomEquality; CustomComparison>]
type LogLevel =
  /// The most verbose log level, more verbose than Debug.
  | Verbose
  /// Less verbose than Verbose, more verbose than Info
  | Debug
  /// Less verbose than Debug, more verbose than Warn
  | Info
  /// Less verbose than Info, more verbose than Error
  | Warn
  /// Less verbose than Warn, more verbose than Fatal
  | Error
  /// The least verbose level. Will only pass through fatal
  /// log lines that cause the application to crash or become
  /// unusable.
  | Fatal
  with
    /// Convert the LogLevel to a string
    override x.ToString () =
      match x with
      | Verbose -> "verbose"
      | Debug -> "debug"
      | Info -> "info"
      | Warn -> "warn"
      | Error -> "error"
      | Fatal -> "fatal"

    /// Converts the string passed to a Loglevel.
    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    static member FromString str =
      match str with
      | "verbose" -> Verbose
      | "debug" -> Debug
      | "info" -> Info
      | "warn" -> Warn
      | "error" -> Error
      | "fatal" -> Fatal
      | _ -> Info

    /// Turn the LogLevel into an integer
    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    member x.ToInt () =
      (function
      | Verbose -> 1
      | Debug -> 2
      | Info -> 3
      | Warn -> 4
      | Error -> 5
      | Fatal -> 6) x

    /// Turn an integer into a LogLevel
    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    static member FromInt i =
      (function
      | 1 -> Verbose
      | 2 -> Debug
      | 3 -> Info
      | 4 -> Warn
      | 5 -> Error
      | 6 -> Fatal
      | _ as i -> failwith "rank %i not available" i) i

    static member op_LessThan (a, b) = (a :> IComparable<LogLevel>).CompareTo(b) < 0
    static member op_LessThanOrEqual (a, b) = (a :> IComparable<LogLevel>).CompareTo(b) <= 0
    static member op_GreaterThan (a, b) = (a :> IComparable<LogLevel>).CompareTo(b) > 0
    static member op_GreaterThanOrEqual (a, b) = (a :> IComparable<LogLevel>).CompareTo(b) >= 0

    override x.Equals other = (x :> IComparable).CompareTo other = 0

    override x.GetHashCode () = x.ToInt ()

    interface IComparable with
      member x.CompareTo other =
        match other with
        | null -> 1
        | :? LogLevel as tother ->
          (x :> IComparable<LogLevel>).CompareTo tother
        | _ -> failwith <| sprintf "invalid comparison %A to %A" x other

    interface IComparable<LogLevel> with
      member x.CompareTo other =
        compare (x.ToInt()) (other.ToInt())

    interface IEquatable<LogLevel> with
      member x.Equals other =
        x.ToInt() = other.ToInt()

type TraceHeader =
  { trace_id       : uint64
    span_id        : uint64
    span_parent_id : uint64 option }
  static member NewTrace(?span_parent_id) =
    { trace_id       = Globals.random.NextUInt64()
      span_id        = Globals.random.NextUInt64()
      span_parent_id = span_parent_id }

/// When logging, write a log line like this with the source of your
/// log line as well as a message and an optional exception.
type LogLine =
    /// the trace id and span id
    /// TODO: support tracing infrastructure, if this is set, then we support
    /// distributed tracing and the LogLine is an annotation instead
  { tracing       : TraceHeader option
    /// the level that this log line has
    level         : LogLevel
    /// the source of the log line, e.g. 'ModuleName.FunctionName'
    path          : string
    /// the message that the application wants to log
    message       : string
    /// an optional exception
    ``exception`` : exn option
    /// timestamp when this log line was created
    ts_utc_ticks  : int64 }

/// The primary Logger abstraction that you can log data into
type Logger =
  /// log - evaluate the function if the log level matches - by making it
  /// a function we don't needlessly need to evaluate it
  abstract member Log : LogLevel -> (unit -> LogLine) -> unit

module Loggers =
  let internal default_formatter (line : LogLine) =
    "[" + Char.ToUpperInvariant(line.ToString().[0]).ToString() + "] " +
    (DateTime(line.ts_utc_ticks, DateTimeKind.Utc).ToString("o")) + ": " +
    line.message + " [" + line.path + "]"

  /// Log a line with the given format, printing the current time in UTC ISO-8601 format
  /// and then the string, like such:
  /// '2013-10-13T13:03:50.2950037Z: today is the day'
  type ConsoleWindowLogger(min_level, ?formatter, ?original_color, ?console_semaphore) =
    let sem            = defaultArg console_semaphore (obj())
    let original_color = defaultArg original_color Console.ForegroundColor
    let formatter      = defaultArg formatter default_formatter

    let to_color = function
      | Verbose -> ConsoleColor.DarkGreen
      | Debug -> ConsoleColor.Green
      | Info -> ConsoleColor.White
      | Warn -> ConsoleColor.Yellow
      | Error -> ConsoleColor.DarkRed
      | Fatal -> ConsoleColor.Red

    let log color line =
      let w = System.Console.WriteLine : string -> unit
      lock sem <| fun _ ->
        Console.ForegroundColor <- color
        (w << formatter) line
        Console.ForegroundColor <- original_color

    interface Logger with
      member x.Log level f = if level >= min_level then log (to_color level) (f ())

let trace f_str =
    System.Console.WriteLine(sprintf "%s: %s" (DateTime.UtcNow.ToString("o")) (f_str ()))

let tracef format =
    format (Printf.kprintf (fun s -> System.Console.WriteLine(sprintf "%s: %s" (DateTime.UtcNow.ToString("o")) s)))

let log str =
  System.Console.WriteLine(sprintf "%s: %s" (DateTime.UtcNow.ToString("o")) str)

let logf format =
  Printf.kprintf
    (fun s -> System.Console.WriteLine(sprintf "%s: %s" (DateTime.UtcNow.ToString("o")) s))
    format

module MayNotUseGlobals =
  type internal LoggingConfiguration =
    { min_level : LogLevel
      factory   : unit -> Logger }

  let configure (min_level : LogLevel) (logger_factory : unit -> Logger) =
    Globals.logging_config := Some (box
      { min_level = min_level
        factory   = logger_factory })

    { new IDisposable with
        member x.Dispose() =
          Globals.logging_config := None }
