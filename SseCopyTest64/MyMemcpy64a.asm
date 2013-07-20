public MyMemcpy64a

STACKBYTES    equ 16*7

.code

SaveRegisters MACRO
    sub rsp,STACKBYTES
   .allocstack STACKBYTES
    movdqu [rsp+16*0],xmm0
   .savexmm128 xmm0, 16*0
    movdqu [rsp+16*1],xmm1
   .savexmm128 xmm1, 16*1
    movdqu [rsp+16*2],xmm2
   .savexmm128 xmm2, 16*2
    movdqu [rsp+16*3],xmm3
   .savexmm128 xmm3, 16*3
    movdqu [rsp+16*4],xmm6
   .savexmm128 xmm6, 16*4
    movdqu [rsp+16*5],xmm7
   .savexmm128 xmm7, 16*5
    mov [rsp+16*6],rsi
   .savereg rsi,16*6
    mov [rsp+16*6+8],rdi
   .savereg rdi,16*6+8
   .endprolog
ENDM

RestoreRegisters MACRO
    movdqu xmm0, [rsp+16*0]
    movdqu xmm1, [rsp+16*1]
    movdqu xmm2, [rsp+16*2]
    movdqu xmm3, [rsp+16*3]
    movdqu xmm6, [rsp+16*4]
    movdqu xmm7, [rsp+16*5]
    mov rsi, [rsp+16*6]
    mov rdi, [rsp+16*6+8]
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

