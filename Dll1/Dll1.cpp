// Dll1.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"
extern "C" __declspec(dllexport) int __stdcall Addfunc(int n1, int n2);

int Addfunc(int a, int b)
{
	return a + b;
}


 class Hand
{
public:
	Hand();
	~Hand();

public:
	static _declspec(dllexport) int add1(int a, int b);
};

Hand::Hand()
{
}

Hand::~Hand()
{
}

 int Hand::add1(int a, int b)
{
	return a+b;
}

