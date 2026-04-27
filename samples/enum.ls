

def OptInt = enum { None, Some(Int value) };

def OnlyGt10 = func(Int x) OptInt :{
    if(x > 10) {
        return OptInt.Some(x);
    } else {
        return OptInt.None;
    }
}

def a = OnlyGt10(5);
def b = OnlyGt10(12);