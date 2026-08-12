// Um `break` incondicional pula o que vem depois dele no corpo do laço.
// expect: output
// 2
// ---
loop {
    break;
    print(1);
}

print(2);
