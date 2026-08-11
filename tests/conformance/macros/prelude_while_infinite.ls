// O primeiro programa LapisLang capaz de não terminar (plano 20 §20.2).
//
// A terminação por construção acabou quando `goto` passou a poder voltar; o que
// sobrou no lugar é uma promessa verificável — todo programa termina **ou**
// reporta `LAP0303`.
// expect: error LAP0303
// expect: abort
@while true {
    def x = 1;
}
