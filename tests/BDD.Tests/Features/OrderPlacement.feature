Feature: 商品訂購流程
  作為一個電商客戶
  我希望可以下單購買商品
  以便完成購物體驗

  Scenario: 成功訂購並扣庫存
    Given 商品 "iPhone 16" 庫存為 100
    And 使用者 "buyer01" 已通過認證
    When 使用者下單購買 1 件 "iPhone 16"
    Then 庫存應減少為 99
    And 訂單狀態應為 "Paid"

  Scenario: 庫存不足時拋出領域例外
    Given 商品 "iPhone 16" 庫存為 0
    When 使用者下單購買 1 件 "iPhone 16"
    Then 應觸發 InsufficientStock 領域例外
