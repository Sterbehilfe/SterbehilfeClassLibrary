using System.Diagnostics.CodeAnalysis;

namespace HLE.Marshalling.Windows;

[SuppressMessage("Design", "CA1069:Enums values should not be duplicated")]
[SuppressMessage("Roslynator", "RCS1234:Duplicate enum value")]
[SuppressMessage("Design", "CA1028:Enum Storage should be Int32")]
public enum VirtualKey : ushort
{
    None = 0x0,
    LeftMouseButton = 0x1,
    RightMouseButton = 0x2,
    ControlBreak = 0x3,
    MiddleMouseButton = 0x4,
    X1 = 0x5,
    X2 = 0x6,
    Backspace = 0x8,
    Tab = 0x9,
    Clear = 0x0C,
    Enter = 0x0D,
    Shift = 0x10,
    Alt = 0x12,
    Pause = 0x13,
    Capslock = 0x14,
    ImeKanaMode = 0x15,
    ImeHangulMode = 0x15,
    ImeOn = 0x16,
    ImeJunjaMode = 0x17,
    ImeFinalMode = 0x18,
    ImeHanjaMode = 0x19,
    ImeKanjiMode = 0x19,
    ImeOff = 0x1A,
    Escape = 0x1B,
    ImeConvert = 0x1C,
    ImeNonConvert = 0x1D,
    ImeAccept = 0x1E,
    ImeModeChangeRequest = 0x1F,
    Space = 0x20,
    PageUp = 0x21,
    PageDown = 0x22,
    End = 0x23,
    Home = 0x24,
    LeftArrow = 0x25,
    UpArrow = 0x26,
    RightArrow = 0x27,
    DownArrow = 0x28,
    Select = 0x29,
    Print = 0x2A,
    Execute = 0x2B,
    PrintScreen = 0x2C,
    Insert = 0x2D,
    Delete = 0x2E,
    Help = 0x2F,
    Zero = 0x30,
    One,
    Two,
    Three,
    Four,
    Five,
    Six,
    Seven,
    Eight,
    Nine,
    A = 0x41,
    B,
    C,
    D,
    E,
    F,
    G,
    H,
    I,
    J,
    K,
    L,
    M,
    N,
    O,
    P,
    Q,
    R,
    S,
    T,
    U,
    V,
    W,
    X,
    Y,
    Z,
    LeftWindows = 0x5B,
    RightWindows = 0x5C,
    Applications = 0x5D,
    ComputerSleep = 0x5F,
    NumpadZero = 0x60,
    NumpadOne,
    NumpadTwo,
    NumpadThree,
    NumpadFour,
    NumpadFive,
    NumpadSix,
    NumpadSeven,
    NumpadEight,
    NumpadNine,
    Multiply = 0x6A,
    Add = 0x6B,
    Separator = 0x6C,
    Subtract = 0x6D,
    Decimal = 0x6E,
    Divide = 0x6F,
    F1 = 0x70,
    F2,
    F3,
    F4,
    F5,
    F6,
    F7,
    F8,
    F9,
    F10,
    F11,
    F12,
    F13,
    F14,
    F15,
    F16,
    F17,
    F18,
    F19,
    F20,
    F21,
    F22,
    F23,
    F24,
    NumLock = 0x90,
    ScrollLock = 0x91,
    LeftShift = 0xA0,
    RightShift = 0xA1,
    LeftControl = 0xA2,
    RightControl = 0xA3,
    LeftAlt = 0xA4,
    RightAlt = 0xA5,
    BrowserBack = 0xA6,
    BrowserForward = 0xA7,
    BrowserRefresh = 0xA8,
    BrowserStop = 0xA9,
    BrowserSearch = 0xAA,
    BrowserFavorites = 0xAB,
    BrowserHome = 0xAC,
    VolumeMute = 0xAD,
    VolumeDown = 0xAE,
    VolumeUp = 0xAF,
    NextTrack = 0xB0,
    PreviousTrack = 0xB1,
    StopMedia = 0xB2,
    PlayPauseMedia = 0xB3,
    StartMail = 0xB4,
    SelectMedia = 0xB5,
    StartApplicationOne = 0xB6,
    StartApplicationTwo = 0xB7,
    OemOne = 0xBA,
    Plus = 0xBB,
    Comma = 0xBC,
    Minus = 0xBD,
    Period = 0xBE,
    OemTwo = 0xBF,
    OemThree = 0xC0,
    OemFour = 0xDB,
    OemFive = 0xDC,
    OemSix = 0xDD,
    OemSeven = 0xDE,
    OemEight = 0xDF,
    Oem102 = 0xE2,
    ImeProcess = 0xE5,
    Packet = 0xE7,
    Attn = 0xF6,
    CrSel = 0xF7,
    ExSel = 0xF8,
    EraseEof = 0xF9,
    Play = 0xFA,
    Zoom = 0xFB,
    Pa1 = 0xFD,
    OemClear = 0xFE
}