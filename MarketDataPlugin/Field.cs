using MarketDataPlugin.DEF;
using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MarketDataPlugin
{
    public class Field : MsgChildDef
    {
        private readonly FieldDef _def;
        private object? _value;
        private string _stringValue = string.Empty;
        private static readonly Regex _notNullDateRecognizer = new Regex("[1-9]");

        public Field(FieldDef def) : base(def.Tag.ToString(), def.Name)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            _value = null;
        }

        public Field(FieldDef def, TVP tvp) : base(def.Tag.ToString(), def.Name)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            _value = null;

            if (tvp == null) return;

            if (!string.IsNullOrEmpty(tvp.Raw) && tvp.Tag != _def.Tag.ToString())
                throw new ArgumentException($"Tag mismatch: Expected {_def.Tag}, got {tvp.Tag}");

            _stringValue = tvp.Value ?? string.Empty;
            ParseValue();
        }

        private void ParseValue()
        {
            if (string.IsNullOrEmpty(_stringValue))
            {
                _value = null;
                return;
            }

            try
            {
                switch (_def.Type)
                {
                    case FieldType.Int:
                        _value = long.Parse(_stringValue);
                        break;
                    case FieldType.Double:
                        _value = double.Parse(_stringValue, CultureInfo.InvariantCulture); // Replaced LocalizeHelper
                        break;
                    case FieldType.Date:
                        if (_notNullDateRecognizer.IsMatch(_stringValue))
                        {
                            DateTime d = DateTime.ParseExact(_stringValue, _def.DateFormat ?? "yyyyMMdd", CultureInfo.InvariantCulture);
                            if (_def.TimeZoneEffected)
                                d = d.AddHours(_def.TimeZone);
                            _value = d;
                        }
                        else
                        {
                            _value = null;
                        }
                        break;
                    default:
                        _value = _stringValue;
                        break;
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    new StringBuilder(Name)
                        .Append("(Tag=")
                        .Append(_def.Tag)
                        .Append(",DataField=")
                        .Append(_def.DataField ?? "N/A")
                        .Append(",Raw=")
                        .Append(_stringValue)
                        .Append(",Type=")
                        .Append(_def.Type)
                        .Append(",Format=")
                        .Append(_def.DateFormat ?? "N/A")
                        .Append(")\r\n")
                        .Append(ex.GetType())
                        .Append(" ")
                        .Append(ex.Message)
                        .ToString(), ex);
            }
        }

        public object? Value => _value;

        public string StringValue
        {
            get
            {
                if (string.IsNullOrEmpty(_stringValue) && _value != null)
                {
                    switch (_def.Type)
                    {
                        case FieldType.Int:
                            _stringValue = _value.ToString();
                            break;
                        case FieldType.Double:
                            _stringValue = ((double)_value).ToString(CultureInfo.InvariantCulture);
                            break;
                        case FieldType.Date:
                            DateTime d = (DateTime)_value;
                            if (_def.TimeZoneEffected)
                                d = ((DateTime)_value).AddHours(-_def.TimeZone);
                            _stringValue = d.ToString(_def.DateFormat ?? "yyyyMMdd");
                            break;
                        default:
                            _stringValue = _value.ToString() ?? string.Empty;
                            break;
                    }
                }
                return _stringValue;
            }
        }

        // Implement the abstract Parse method from FixItem
        public override void Parse(string fixMessage)
        {
            throw new NotImplementedException("Parse method is not supported for one-way parsing. Use constructor with TVP instead.");
        }

        // Removed two-way properties: Changed, Raw, LogString, and parent reference
    }
}