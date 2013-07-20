public MyMemcpy64a

STACKBYTES    equ 16*3

.code

SaveRegisters MACRO
    sub rsp,STACKBYTES
   .allocstack STACKBYTES
    movdqu [rsp+16*0],xmm6
   .savexmm128 xmm6, 16*0
    movdqu [rsp+16*1],xmm7
   .savexmm128 xmm7, 16*1
    mov [rsp+16*2],rsi
   .savereg rsi,16*2
    mov [rsp+16*2+8],rdi
   .savereg rdi,16*2+8
   .endprolog
ENDM

RestoreRegisters MACRO
    movdqu xmm6, [rsp+16*0]
    movdqu xmm7, [rsp+16*1]
    mov rsi, [rsp+16*2]
    mov rdi, [rsp+16*2+8]
    add rsp,STACKBYTES
ENDM

; MyMemcpy64a(char *dst, const char *src, int bytes)
; dst : rcx
; src : rdx
; bytes : r8d
align 8
MyMemcpy64a proc frame
    SaveRegisters
    mov rsi, rdx ; src pointer
    mov rdi, rcx ; dest pointer
    mov ecx, r8d ; our counter 
    shr rcx, 7   ; divide by 128 (8 * 128bit registers)
align 8
LabelBegin:
    prefetchnta 128[esi]
    prefetchnta 160[esi]
    prefetchnta 192[esi]
    prefetchnta 224[esi]

    movdqa xmm0, 0[esi]
    movdqa xmm1, 16[esi]
    movdqa xmm2, 32[esi]
    movdqa xmm3, 48[esi]
    movdqa xmm4, 64[esi]
    movdqa xmm5, 80[esi]
    movdqa xmm6, 96[esi]
    movdqa xmm7, 112[esi]

    movntdq 0[edi],  xmm0
    movntdq 16[edi], xmm1
    movntdq 32[edi], xmm2
    movntdq 48[edi], xmm3
    movntdq 64[edi], xmm4
    movntdq 80[edi], xmm5
    movntdq 96[edi], xmm6
    movntdq 112[edi], xmm7

    add esi, 128
    add edi, 128
    dec ecx
    jnz LabelBegin
    RestoreRegisters
    ret
align 8
MyMemcpy64a endp
end

