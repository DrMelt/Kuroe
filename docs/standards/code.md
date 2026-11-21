# 代码规范

- 禁止使用 null 包容运算符抑制可空警告。
- 名称表达职责，遵循 Intention-Revealing Names：data、info、manager、helper 这类不承载职责的泛称是职责不清的症状。
- 逻辑归属由职责决定，遵循 Information Expert：被调用方的规则由其实现维护，调用方只保留自身业务决策。