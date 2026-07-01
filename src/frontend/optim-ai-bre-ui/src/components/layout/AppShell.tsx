'use client'

import React, { useState } from 'react'
import Link from 'next/link'
import { usePathname } from 'next/navigation'

const NAV_ITEMS = [
  { href: '/', label: 'Dashboard', icon: '📊' },
  { href: '/rules', label: 'Rule Designer', icon: '⚙️' },
  { href: '/rule-sets', label: 'Rule Sets', icon: '📦' },
  { href: '/sandbox', label: 'Sandbox', icon: '🧪' },
  { href: '/decisions', label: 'Decisions', icon: '⚖️' },
  { href: '/deviations', label: 'Deviations', icon: '⚠️' },
  { href: '/ai-rules', label: 'AI Rule Creator', icon: '🤖' },
  { href: '/marketplace', label: 'Marketplace', icon: '🛒' },
  { href: '/clients', label: 'Clients', icon: '🏢' },
  { href: '/reports', label: 'Reports', icon: '📄' },
  { href: '/audit', label: 'Audit Trail', icon: '🔍' },
  { href: '/settings', label: 'Settings', icon: '🔧' },
]

export function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname()
  const [collapsed, setCollapsed] = useState(false)

  // Don't show shell on login page
  if (pathname === '/login') return <>{children}</>

  return (
    <div className="flex h-screen bg-gray-50 overflow-hidden">
      {/* Sidebar */}
      <aside className={`${collapsed ? 'w-16' : 'w-56'} flex flex-col bg-gray-900 text-white transition-all duration-200 flex-shrink-0`}>
        {/* Logo */}
        <div className="flex items-center gap-3 px-4 py-5 border-b border-gray-700">
          <div className="w-8 h-8 rounded-lg bg-blue-500 flex items-center justify-center text-sm font-black">O</div>
          {!collapsed && (
            <div>
              <div className="text-sm font-bold leading-tight">OPTIM AI</div>
              <div className="text-xs text-gray-400">BRE Engine</div>
            </div>
          )}
          <button
            onClick={() => setCollapsed(!collapsed)}
            className="ml-auto text-gray-400 hover:text-white text-xs"
          >
            {collapsed ? '▶' : '◀'}
          </button>
        </div>

        {/* Nav Items */}
        <nav className="flex-1 py-4 overflow-y-auto">
          {NAV_ITEMS.map(({ href, label, icon }) => {
            const isActive = pathname === href || (href !== '/' && pathname.startsWith(href))
            return (
              <Link
                key={href}
                href={href}
                className={`flex items-center gap-3 px-4 py-2.5 mx-2 rounded-lg transition-colors text-sm ${
                  isActive
                    ? 'bg-blue-600 text-white font-medium'
                    : 'text-gray-400 hover:bg-gray-800 hover:text-white'
                }`}
                title={collapsed ? label : undefined}
              >
                <span className="text-base flex-shrink-0">{icon}</span>
                {!collapsed && <span>{label}</span>}
              </Link>
            )
          })}
        </nav>

        {/* Footer */}
        <div className="border-t border-gray-700 px-4 py-3">
          {!collapsed && (
            <div className="flex items-center gap-2">
              <div className="w-7 h-7 rounded-full bg-blue-500 flex items-center justify-center text-xs font-bold">A</div>
              <div>
                <div className="text-xs font-medium text-white">Admin User</div>
                <div className="text-xs text-gray-400">Super Admin</div>
              </div>
            </div>
          )}
        </div>
      </aside>

      {/* Main Content */}
      <main className="flex-1 overflow-auto">{children}</main>
    </div>
  )
}
