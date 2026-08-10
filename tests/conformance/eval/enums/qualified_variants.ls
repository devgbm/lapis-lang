// expect: output
// Color.Red
// true
// false
// ---
def Color = enum { Red, Green };

print(Color.Red);
print(Color.Red == Color.Red);
print(Color.Red == Color.Green);
