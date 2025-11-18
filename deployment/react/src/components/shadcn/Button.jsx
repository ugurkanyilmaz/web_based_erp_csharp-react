import React from 'react'

export default function Button({ children, variant = 'primary', className = '', ...props }) {
  const base = 'inline-flex items-center justify-center px-4 py-2 rounded-md font-medium focus:outline-none';
  const variants = {
    primary: 'bg-primary text-white hover:bg-primary-focus',
    ghost: 'bg-transparent border border-transparent hover:bg-slate-100',
    outline: 'btn btn-outline',
  }
  const cls = `${base} ${variants[variant] ?? variants.primary} ${className}`;
  return (
    <button className={cls} {...props}>{children}</button>
  )
}
